#if !NETSTANDARD2_1
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;
using Debug = System.Diagnostics.Debug;
using MemoryMarshal = System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.Threading
{
    using DotNext;
    using Tasks;

    public partial class AsyncCorrelationSource<TKey, TValue>
    {
        private partial class WaitNode : ValueTaskCompletionSource<TValue>
        {
            private readonly WeakReference<ConcurrentBag<WaitNode>> poolRef;
            internal TKey? Id;
            internal volatile Bucket? Owner;

            internal WaitNode(ConcurrentBag<WaitNode> pool)
                => poolRef = new(pool, false);

            protected override void BeforeCompleted(Result<TValue> result)
                => Interlocked.Exchange(ref Owner, null)?.Remove(this);

            protected override void AfterConsumed()
            {
                // return node back to the pool
                if (poolRef.TryGetTarget(out var target))
                {
                    target.Add(this);
                }
            }

            internal partial WaitNode? CleanupAndGotoNext()
            {
                // This method can be concurrently called with BeforeCompleted.
                // However, the method is called inside of monitor acquired on
                // the whole bucket itself. As a result, Remove method is blocked
                Owner = null;
                var next = this.next;
                this.next = previous = null;
                return next;
            }
        }

        private readonly ConcurrentBag<WaitNode> pool = new();

        private partial Bucket GetBucket(TKey eventId)
        {
            var bucketIndex = unchecked((uint)comparer.GetHashCode(eventId)) % buckets.LongLength;
            Debug.Assert(bucketIndex >= 0 && bucketIndex < buckets.LongLength);

            return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buckets), (nint)bucketIndex);
        }

        private partial ValueTask<TValue> WaitCoreAsync(TKey eventId, TimeSpan timeout, CancellationToken token)
        {
            if (timeout != InfiniteTimeSpan && timeout < TimeSpan.Zero)
                return ValueTask.FromException<TValue>(new ArgumentOutOfRangeException(nameof(timeout)));
            if (token.IsCancellationRequested)
                return ValueTask.FromCanceled<TValue>(token);

            // take the task source from the pool
            if (!pool.TryTake(out var node))
                node = new(pool);

            var bucket = GetBucket(eventId);

            // initialize node
            node.Reset();
            node.Id = eventId;
            node.Owner = bucket;

            // we need to add the node to the list before the task construction
            // to ensure that completed node will not be added to the list due to cancellation
            bucket.Add(node);

            return node.CreateTask(timeout, token);
        }

        /// <summary>
        /// Removes all cached tasks.
        /// </summary>
        public void ClearCache() => pool.Clear();
    }
}
#endif