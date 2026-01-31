using static System.Threading.Timeout;

namespace DotNext.Threading;

using Numerics;
using Tasks;

/// <summary>
/// Represents pub/sub synchronization primitive
/// when each event has unique identifier.
/// </summary>
/// <remarks>
/// This synchronization primitive is useful when you need to correlate
/// two events across process boundaries. For instance, you can send asynchronous
/// message to another process or machine in the network and wait for the response.
/// The message passing is not a duplex operation (in case of message brokers)
/// so you need to wait for another input message and identify that this message
/// is a response. These two messages can be correlated with the key.
/// The consumer and producer of the event must be protected by happens-before semantics.
/// It means that the call to <see cref="WaitAsync(TKey, object?, TimeSpan, CancellationToken)"/> by the consumer must happen
/// before the call to <see cref="Pulse(TKey, TValue)"/> by the producer for the same key.
/// </remarks>
/// <typeparam name="TKey">The type of the event identifier.</typeparam>
/// <typeparam name="TValue">The type of the event payload.</typeparam>
public partial class AsyncCorrelationSource<TKey, TValue>
    where TKey : notnull
{
    private readonly FastMod fastMod;
    private readonly Bucket?[] buckets;
    private readonly IEqualityComparer<TKey>? comparer; // if null then use Default comparer

    /// <summary>
    /// Initializes a new event correlation source.
    /// </summary>
    /// <param name="concurrencyLevel">The number of events that can be processed without blocking at the same time.</param>
    /// <param name="comparer">The comparer to be used for comparison of the keys.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than or equal to zero.</exception>
    public AsyncCorrelationSource(int concurrencyLevel, IEqualityComparer<TKey>? comparer = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concurrencyLevel);

        concurrencyLevel = PrimeNumber.GetPrime(concurrencyLevel);
        fastMod = new((uint)concurrencyLevel);
        buckets = new Bucket[concurrencyLevel];
        this.comparer = comparer;
    }

    /// <summary>
    /// Informs that the event is occurred.
    /// </summary>
    /// <remarks>
    /// If no listener present for <paramref name="eventId"/> then the signal will be dropped.
    /// </remarks>
    /// <param name="eventId">The unique identifier of the event.</param>
    /// <param name="result">The value to be passed to the listener.</param>
    /// <returns><see langword="true"/> if there is an active listener of this event; <see langword="false"/>.</returns>
    public bool Pulse(TKey eventId, TValue result)
        => Pulse<Result<TValue>.Ok>(eventId, result, out _);

    /// <summary>
    /// Informs that the event is occurred.
    /// </summary>
    /// <remarks>
    /// If no listener present for <paramref name="eventId"/> then the signal will be dropped.
    /// </remarks>
    /// <param name="eventId">The unique identifier of the event.</param>
    /// <param name="e">The exception to be passed to the listener.</param>
    /// <returns><see langword="true"/> if there is an active listener of this event; <see langword="false"/>.</returns>
    public bool Pulse(TKey eventId, Exception e)
        => Pulse<Result<TValue>.Failure>(eventId, e, out _);

    /// <summary>
    /// Informs that the event is occurred.
    /// </summary>
    /// <remarks>
    /// If no listener present for <paramref name="eventId"/> then the signal will be dropped.
    /// </remarks>
    /// <param name="eventId">The unique identifier of the event.</param>
    /// <param name="result">The value to be passed to the listener.</param>
    /// <param name="userData">Custom data associated with an event.</param>
    /// <returns><see langword="true"/> if there is an active listener of this event; <see langword="false"/>.</returns>
    public bool Pulse(TKey eventId, TValue result, out object? userData)
        => Pulse<Result<TValue>.Ok>(eventId, result, out userData);

    /// <summary>
    /// Informs that the event is occurred.
    /// </summary>
    /// <remarks>
    /// If no listener present for <paramref name="eventId"/> then the signal will be dropped.
    /// </remarks>
    /// <param name="eventId">The unique identifier of the event.</param>
    /// <param name="e">The exception to be passed to the listener.</param>
    /// <param name="userData">Custom data associated with an event.</param>
    /// <returns><see langword="true"/> if there is an active listener of this event; <see langword="false"/>.</returns>
    public bool Pulse(TKey eventId, Exception e, out object? userData)
        => Pulse<Result<TValue>.Failure>(eventId, e, out userData);
    
    private bool Pulse<TResult>(TKey eventId, TResult value, out object? userData)
        where TResult : struct, IResultMonad<TValue>
    {
        bool result;
        var bucket = Volatile.Read(ref GetBucket(eventId));

        if (bucket?.Remove(eventId, comparer, out var completionToken) is { } node)
        {
            userData = node.UserData;
            result = node.TrySetResult(new ManualResetCompletionSource.ExpectedSourceTokenAndSentinel(completionToken), value, out var resumable);
            if (resumable)
                node.NotifyConsumer();
        }
        else
        {
            result = false;
            userData = null;
        }

        return result;
    }

    private void PulseAll<TResult>(TResult arg)
        where TResult : struct, IResultMonad<TValue>
    {
        foreach (ref var bucket in buckets.AsSpan())
            Volatile.Read(ref bucket)?.Drain(arg);
    }

    /// <summary>
    /// Notifies all active listeners.
    /// </summary>
    /// <param name="value">The value to be passed to all active listeners.</param>
    public void PulseAll(TValue value)
        => PulseAll<Result<TValue>.Ok>(value);

    /// <summary>
    /// Raises the exception on all active listeners.
    /// </summary>
    /// <param name="e">The exception to be passed to all active listeners.</param>
    public void PulseAll(Exception e)
        => PulseAll<Result<TValue>.Failure>(e);

    /// <summary>
    /// Cancels all active listeners.
    /// </summary>
    /// <param name="token">The token in the canceled state.</param>
    public void PulseAll(CancellationToken token)
        => PulseAll(new OperationCanceledException(token));

    /// <summary>
    /// Returns the task linked with the specified event identifier.
    /// </summary>
    /// <param name="eventId">The unique identifier of the event.</param>
    /// <param name="userData">Custom data associated with the event.</param>
    /// <param name="timeout">The time to wait for <see cref="Pulse(TKey, TValue, out object)"/>.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing the event arrival.</returns>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<TValue> WaitAsync(TKey eventId, object? userData, TimeSpan timeout, CancellationToken token = default)
    {
        return !Timeout.IsValid(timeout)
            ? ValueTask.FromException<TValue>(new ArgumentOutOfRangeException(nameof(timeout)))
            : token.IsCancellationRequested
            ? ValueTask.FromCanceled<TValue>(token)
            : EnsureInitialized(ref GetBucket(eventId)).CreateNode(eventId, userData).CreateTask(timeout, token);

        // we are not using LazyInitializer to avoid try-catch block
        static Bucket EnsureInitialized(ref Bucket? bucket)
        {
            Bucket newBucket;
            return Volatile.Read(ref bucket) ?? Interlocked.CompareExchange(ref bucket, newBucket = new(), null) ?? newBucket;
        }
    }
    
    /// <summary>
    /// Returns the task linked with the specified event identifier.
    /// </summary>
    /// <param name="eventId">The unique identifier of the event.</param>
    /// <param name="timeout">The time to wait for <see cref="Pulse(TKey, TValue)"/>.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing the event arrival.</returns>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<TValue> WaitAsync(TKey eventId, TimeSpan timeout, CancellationToken token = default)
        => WaitAsync(eventId, null, timeout, token);

    /// <summary>
    /// Returns the task linked with the specified event identifier.
    /// </summary>
    /// <param name="eventId">The unique identifier of the event.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing the event arrival.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<TValue> WaitAsync(TKey eventId, CancellationToken token = default)
        => WaitAsync(eventId, InfiniteTimeSpan, token);
}