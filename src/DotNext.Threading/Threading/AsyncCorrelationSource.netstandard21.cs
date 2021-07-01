#if NETSTANDARD2_1
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading
{
    using Tasks;
    using CallerMustBeSynchronizedAttribute = Runtime.CompilerServices.CallerMustBeSynchronizedAttribute;

    public partial class AsyncCorrelationSource<TKey, TValue>
    {
        private partial class WaitNode : TaskCompletionSource<TValue>
        {
            internal readonly TKey Id;

            internal WaitNode(TKey key)
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
                => Id = key;

            internal partial WaitNode? CleanupAndGotoNext()
            {
                var next = this.next;
                this.next = previous = null;
                return next;
            }
        }

        private partial class Bucket
        {
            internal async Task<TValue> WaitAsync(WaitNode node, TimeSpan timeout, CancellationToken token)
            {
                using (var source = token.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(token) : new CancellationTokenSource())
                {
                    if (ReferenceEquals(node.Task, await Task.WhenAny(node.Task, Task.Delay(timeout, source.Token)).ConfigureAwait(false)))
                    {
                        source.Cancel(); // ensure that timer is cancelled
                        goto exit;
                    }
                }

                if (Remove(node))
                {
                    token.ThrowIfCancellationRequested();
                    throw new TimeoutException();
                }

                exit:
                return await node.Task.ConfigureAwait(false);
            }

            internal async Task<TValue> WaitAsync(WaitNode node, CancellationToken token)
            {
                Debug.Assert(token.CanBeCanceled);
                using (var source = new CancelableCompletionSource<TValue>(TaskCreationOptions.RunContinuationsAsynchronously, token))
                {
                    if (ReferenceEquals(node.Task, await Task.WhenAny(node.Task, source.Task).ConfigureAwait(false)))
                        goto exit;
                }

                if (Remove(node))
                    token.ThrowIfCancellationRequested();

                exit:
                return await node.Task.ConfigureAwait(false);
            }
        }

        private partial Bucket GetBucket(TKey eventId)
        {
            var bucketIndex = unchecked((uint)comparer.GetHashCode(eventId)) % buckets.LongLength;
            Debug.Assert(bucketIndex >= 0 && bucketIndex < buckets.LongLength);

            return buckets[bucketIndex];
        }

        private partial ValueTask<TValue> WaitCoreAsync(TKey eventId, TimeSpan timeout, CancellationToken token)
        {
            if (timeout != InfiniteTimeSpan && timeout < TimeSpan.Zero)
                return new(Task.FromException<TValue>(new ArgumentOutOfRangeException(nameof(timeout))));
            if (token.IsCancellationRequested)
                return new(Task.FromCanceled<TValue>(token));

            var node = new WaitNode(eventId);
            var bucket = GetBucket(eventId);
            bucket.Add(node);

            return new(timeout.CompareTo(TimeSpan.Zero) switch
            {
                0 => Task.FromException<TValue>(new TimeoutException()),
                > 0 => bucket.WaitAsync(node, timeout, token),
                _ when token.CanBeCanceled => bucket.WaitAsync(node, token),
                _ => node.Task,
            });
        }
    }
}
#endif