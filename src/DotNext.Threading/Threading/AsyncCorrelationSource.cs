using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading
{
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
    /// It means that the call to <see cref="WaitAsync(TKey, TimeSpan, CancellationToken)"/> by the consumer must happen
    /// before the call to <see cref="TrySignal(TKey, TValue)"/> by the producer for the same key.
    /// </remarks>
    /// <typeparam name="TKey">The type of the event identifier.</typeparam>
    /// <typeparam name="TValue">The type of the event payload.</typeparam>
    public partial class AsyncCorrelationSource<TKey, TValue>
        where TKey : notnull
    {
        private readonly Bucket[] buckets;
        private readonly IEqualityComparer<TKey> comparer;

        /// <summary>
        /// Initializes a new event correlation source.
        /// </summary>
        /// <param name="concurrencyLevel">The number of events that can be processed without blocking at the same time.</param>
        /// <param name="comparer">The comparer to be used for comparison of the keys.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than or equal to zero.</exception>
        public AsyncCorrelationSource(long concurrencyLevel, IEqualityComparer<TKey>? comparer = null)
        {
            if (concurrencyLevel <= 0L)
                throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));

            buckets = new Bucket[concurrencyLevel];

            for (var i = 0L; i < concurrencyLevel; i++)
                buckets[i] = new();

            this.comparer = comparer ?? EqualityComparer<TKey>.Default;
        }

        private partial Bucket GetBucket(TKey eventId);

        /// <summary>
        /// Informs that the event is occurred.
        /// </summary>
        /// <remarks>
        /// If no listener present for <paramref name="eventId"/> then the signal will be dropped.
        /// </remarks>
        /// <param name="eventId">The unique identifier of the event.</param>
        /// <param name="value">The value to be passed to the listener.</param>
        /// <returns><see langword="true"/> if the is an active listener of this event; <see langword="false"/>.</returns>
        public bool TrySignal(TKey eventId, TValue value)
            => GetBucket(eventId).Remove(eventId, value, comparer);

        private unsafe void PulseAll<T>(delegate*<WaitNode, T, void> action, T arg)
        {
            Debug.Assert(action != null);

            foreach (var bucket in buckets)
                bucket.Drain(action, arg);
        }

        /// <summary>
        /// Notifies all active listeners.
        /// </summary>
        /// <param name="value">The value to be passed to all active listeners.</param>
        public unsafe void PulseAll(TValue value)
        {
            PulseAll(&SetResult, value);

            static void SetResult(WaitNode slot, TValue value) => slot.TrySetResult(value);
        }

        /// <summary>
        /// Raises the exception on all active listeners.
        /// </summary>
        /// <param name="e">The exception to be passed to all active listeners.</param>
        public unsafe void PulseAll(Exception e)
        {
            PulseAll(&SetException, e);

            static void SetException(WaitNode slot, Exception e) => slot.TrySetException(e);
        }

        /// <summary>
        /// Cancels all active listeners.
        /// </summary>
        /// <param name="token">The token in the canceled state.</param>
        public unsafe void PulseAll(CancellationToken token)
        {
            PulseAll(&SetCanceled, token);

            static void SetCanceled(WaitNode slot, CancellationToken token) => slot.TrySetCanceled(token);
        }

        // TODO: Workaround for Roslyn analyzers
        private partial ValueTask<TValue> WaitCoreAsync(TKey eventId, TimeSpan timeout, CancellationToken token);

        /// <summary>
        /// Returns the task linked with the specified event identifier.
        /// </summary>
        /// <param name="eventId">The unique identifier of the event.</param>
        /// <param name="timeout">The time to wait for <see cref="TrySignal(TKey, TValue)"/>.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing the event arrival.</returns>
        /// <exception cref="TimeoutException">The operation has timed out.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public ValueTask<TValue> WaitAsync(TKey eventId, TimeSpan timeout, CancellationToken token = default)
            => WaitCoreAsync(eventId, timeout, token);

        /// <summary>
        /// Returns the task linked with the specified event identifier.
        /// </summary>
        /// <param name="eventId">The unique identifier of the event.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing the event arrival.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public ValueTask<TValue> WaitAsync(TKey eventId, CancellationToken token = default)
            => WaitCoreAsync(eventId, InfiniteTimeSpan, token);
    }
}