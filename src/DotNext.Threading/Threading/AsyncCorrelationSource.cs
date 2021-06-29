using System;
using System.Collections.Generic;
using System.Threading;
using Debug = System.Diagnostics.Debug;
using MemoryMarshal = System.Runtime.InteropServices.MemoryMarshal;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

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
    /// It means that the call to <see cref="Listen(TKey)"/> by the consumer must happen
    /// before the call to <see cref="TrySignal(TKey, TValue)"/> by the producer for the same key.
    /// </remarks>
    /// <typeparam name="TKey">The type of the event identifier.</typeparam>
    /// <typeparam name="TValue">The type of the event payload.</typeparam>
    public partial class AsyncCorrelationSource<TKey, TValue>
        where TKey : notnull
    {
        // TODO: In future versions we can reuse completion sources from object pool. But we need support of CancellationTokenSource.TryReset()
        // and track the special numeric token for cancellation. Also, we could change the return type from Task to ValueTask
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

        private Bucket GetBucket(TKey eventId)
        {
            var bucketIndex = unchecked((uint)comparer.GetHashCode(eventId)) % buckets.LongLength;
            Debug.Assert(bucketIndex >= 0 && bucketIndex < buckets.LongLength);

#if NETSTANDARD2_1
            return buckets[bucketIndex];
#else
            // skip bounds check
            return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buckets), (nint)bucketIndex);
#endif
        }

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
        {
            var bucket = GetBucket(eventId);
            lock (bucket)
            {
                for (var current = bucket.First; current is not null; current = current.Next)
                {
                    // notify the listener and remove it immediately
                    if (current.Value.TrySetResult(eventId, value))
                    {
                        bucket.Remove(current);
                        return true;
                    }
                }
            }

            return false;
        }

        private unsafe void PulseAll<T>(T arg, delegate*<Slot, T, void> action)
        {
            foreach (var bucket in buckets)
            {
                lock (bucket)
                {
                    for (LinkedListNode<Slot>? current = bucket.First, next; current is not null; current = next)
                    {
                        next = current.Next;
                        action(current.Value, arg);
                        bucket.Remove(current);
                    }
                }
            }
        }

        /// <summary>
        /// Notifies all active listeners.
        /// </summary>
        /// <param name="value">The value to be passed to all active listeners.</param>
        public unsafe void PulseAll(TValue value)
        {
            PulseAll(value, &SetResult);

            static void SetResult(Slot slot, TValue value) => slot.TrySetResult(value);
        }

        /// <summary>
        /// Raises the exception on all active listeners.
        /// </summary>
        /// <param name="e">The exception to be passed to all active listeners.</param>
        public unsafe void PulseAll(Exception e)
        {
            PulseAll(e, &SetException);

            static void SetException(Slot slot, Exception e) => slot.TrySetException(e);
        }

        /// <summary>
        /// Cancels all active listeners.
        /// </summary>
        /// <param name="token">The token in the canceled state.</param>
        public unsafe void PulseAll(CancellationToken token)
        {
            PulseAll(token, &SetCanceled);

            static void SetCanceled(Slot slot, CancellationToken token) => slot.TrySetCanceled(token);
        }

        /// <summary>
        /// Creates a listener for a signal with the specified identifier.
        /// </summary>
        /// <param name="eventId">The unique identifier of the event.</param>
        /// <returns>The listener.</returns>
        public Listener Listen(TKey eventId)
        {
            var bucket = GetBucket(eventId);
            LinkedListNode<Slot> slotHolder;

            lock (bucket)
            {
                slotHolder = bucket.AddLast(new Slot(eventId, comparer));
            }

            return new(slotHolder);
        }
    }
}