using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static System.Threading.Timeout;

namespace DotNext.Threading;

using Tasks;
using Tasks.Pooling;

/// <summary>
/// Represents a synchronization point at which two async flows can cooperate and swap elements
/// within pairs.
/// </summary>
/// <remarks>
/// This type is useful to organize pipeline between consumer and producer.
/// </remarks>
/// <typeparam name="T">The type of objects that may be exchanged.</typeparam>
[DebuggerDisplay($"CanBeCompletedSynchronously = {{{nameof(CanBeCompletedSynchronously)}}}, Terminated = {{{nameof(IsTerminated)}}}")]
public class AsyncExchanger<T> : Disposable, IAsyncDisposable
{
    private sealed class ExchangePoint : LinkedValueTaskCompletionSource<T>, IPooledManualResetCompletionSource<Action<ExchangePoint>>
    {
        private Action<ExchangePoint>? consumedCallback;
        internal T? Value;

        protected override void AfterConsumed() => consumedCallback?.Invoke(this);

        private protected override void ResetCore()
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                Value = default;

            base.ResetCore();
        }

        internal bool TryExchange(ref T value)
        {
            if (TrySetResult(value))
            {
                value = Value!;
                return true;
            }

            return false;
        }

        ref Action<ExchangePoint>? IPooledManualResetCompletionSource<Action<ExchangePoint>>.OnConsumed => ref consumedCallback;
    }

    private readonly TaskCompletionSource disposeTask;
    private ValueTaskPool<T, ExchangePoint, Action<ExchangePoint>> pool;
    private ExchangePoint? point;
    private volatile ExchangeTerminatedException? termination;

    /// <summary>
    /// Initializes a new asynchronous exchanger.
    /// </summary>
    public AsyncExchanger()
    {
        pool = new(RemoveExchangePoint);
        disposeTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    [ExcludeFromCodeCoverage]
    private bool CanBeCompletedSynchronously => point is { IsCompleted: false };

    private ExchangePoint RentExchangePoint(T value)
    {
        Debug.Assert(Monitor.IsEntered(this));

        var result = pool.Get();
        result.Value = value;
        return result;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void RemoveExchangePoint(ExchangePoint point)
    {
        if (ReferenceEquals(this.point, point))
            this.point = null;

        pool.Return(point);
    }

    /// <summary>
    /// Waits for another flow to arrive at this exchange point,
    /// then transfers the given object to it, receiving its object as return value.
    /// </summary>
    /// <param name="value">The object to exchange.</param>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The object provided by another async flow.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled or timed out.</exception>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="ExchangeTerminatedException">The exhange has been terminated.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public ValueTask<T> ExchangeAsync(T value, TimeSpan timeout, CancellationToken token = default)
    {
        ValueTask<T> result;

        if (IsDisposed)
        {
            result = new(GetDisposedTask<T>());
        }
        else if (IsDisposing)
        {
            Dispose(true);
            result = new(GetDisposedTask<T>());
        }
        else if (termination is not null)
        {
            result = ValueTask.FromException<T>(termination);
        }
        else if (point?.TryExchange(ref value) ?? false)
        {
            point = null;
            result = new(value);
        }
        else
        {
            point = RentExchangePoint(value);
            result = point.CreateTask(timeout, token);
        }

        return result;
    }

    /// <summary>
    /// Waits for another flow to arrive at this exchange point,
    /// then transfers the given object to it, receiving its object as return value.
    /// </summary>
    /// <param name="value">The object to exchange.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The object provided by another async flow.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="ExchangeTerminatedException">The exhange has been terminated.</exception>
    public ValueTask<T> ExchangeAsync(T value, CancellationToken token = default)
        => ExchangeAsync(value, InfiniteTimeSpan, token);

    /// <summary>
    /// Attempts to transfer the object to another flow synchronously.
    /// </summary>
    /// <remarks>
    /// <paramref name="value"/> remains unchanged if return value is <see langword="false"/>.
    /// </remarks>
    /// <param name="value">The object to exchange.</param>
    /// <returns><see langword="true"/> if another flow is ready for exchange; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="ExchangeTerminatedException">The exhange has been terminated.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool TryExchange(ref T value)
    {
        ThrowIfDisposed();

        bool result;
        if (IsDisposing)
        {
            Dispose(true);
            result = false;
        }
        else if (termination is not null)
        {
            throw termination;
        }
        else if (point is null)
        {
            result = false;
        }
        else
        {
            result = point.TryExchange(ref value);
            point = null;
        }

        return result;
    }

    /// <summary>
    /// Informs another participant that no more data will be exchanged with it.
    /// </summary>
    /// <param name="exception">The optional exception indicating termination reason.</param>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The exchange is already terminated.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Terminate(Exception? exception = null)
    {
        ThrowIfDisposed();
        if (termination is not null)
            throw new InvalidOperationException();

        ExchangeTerminatedException tmp;
        termination = tmp = new ExchangeTerminatedException(exception);
        if (point?.TrySetException(tmp) ?? false)
            point = null;
    }

    /// <summary>
    /// Gets a value indicating whether this exchange has been terminated.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    public bool IsTerminated
    {
        get
        {
            ThrowIfDisposed();
            return termination is not null;
        }
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void NotifyObjectDisposed()
    {
        point?.TrySetException(new ObjectDisposedException(GetType().Name));
        point = null;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            NotifyObjectDisposed();
            termination = null;
            disposeTask.TrySetResult();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.Synchronized)]
    protected override ValueTask DisposeAsyncCore()
    {
        if (point is null or { IsCompleted: true })
        {
            Dispose(true);
            return ValueTask.CompletedTask;
        }

        return new(disposeTask.Task);
    }

    /// <summary>
    /// Provides graceful shutdown of this instance.
    /// </summary>
    /// <returns>The task representing state of asynchronous graceful shutdown.</returns>
    public new ValueTask DisposeAsync() => base.DisposeAsync();
}