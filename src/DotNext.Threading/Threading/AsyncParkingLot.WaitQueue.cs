using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Tasks;
using Tasks.Pooling;

partial class AsyncParkingLot
{
    [StructLayout(LayoutKind.Auto)]
    private struct WaitQueue(long? maximumRetained)
    {
        internal readonly System.Threading.Lock Lock = new();
        private LinkedValueTaskCompletionSource<bool>.LinkedList queue;
        private int length;

        private ValueTaskPool<bool> pool = maximumRetained.HasValue
            ? new(maximumRetained.GetValueOrDefault())
            : new();

        private readonly WaitNode? First => queue.First as WaitNode;

        public void Remove(WaitNode node, in TagList measurementTags)
        {
            Debug.Assert(Lock.IsHeldByCurrentThread);
            
            SuspendedCallersMeter.Add(-1, in measurementTags);
            length--;

            Debug.Assert(length >= 0L);
            queue.Remove(node);
        }

        // Signals the first live waiter for the position. The node completed by the timeout or cancellation
        // is skipped and left in the queue, its consumer removes it (see ReturnNode)
        public WaitNode? TrySignalFirst(int position, object? userData, in TagList measurementTags, out bool hasMore, out bool resumable)
        {
            Debug.Assert(Lock.IsHeldByCurrentThread);

            var result = TrySignalFirst(position, userData, in measurementTags, out WaitNode? nextNode, out resumable);
            hasMore = nextNode is not null && HasMore(nextNode, position, userData);

            return result;

            static bool HasMore([DisallowNull] WaitNode? startPos, int position, object? userData)
            {
                if (!startPos.Matches(position, userData))
                {
                    do
                    {
                        startPos = Unsafe.As<WaitNode>(startPos.Next);
                    } while (startPos?.Matches(position, userData) is false);
                }

                return startPos is not null;
            }
        }
        
        private WaitNode? TrySignalFirst(int position, object? userData, in TagList measurementTags, out WaitNode? nextNode, out bool resumable)
        {
            Debug.Assert(Lock.IsHeldByCurrentThread);

            for (WaitNode? current = First, next; current is not null; current = next)
            {
                next = Unsafe.As<WaitNode>(current.Next); // Remove() detaches the node, capture the link first

                if (current.Matches(position, userData)
                    && current.TrySetResult(new ManualResetCompletionSource.CustomCompletionData(Sentinel.Instance), Result.True, out resumable))
                {
                    Remove(current, in measurementTags);
                    nextNode = next;
                    return current;
                }
            }

            resumable = false;
            nextNode = null;
            return null;
        }

        public void Add(WaitNode node, in TagList measurementTags)
        {
            Debug.Assert(Lock.IsHeldByCurrentThread);
            
            SuspendedCallersMeter.Add(1, in measurementTags);
            queue.Add(node);
            length++;
        }

        public WaitNode RentFromPool()
        {
            Debug.Assert(Lock.IsHeldByCurrentThread);
            
            return pool.Rent<WaitNode>();
        }

        public void ReturnToPool(WaitNode node)
        {
            Debug.Assert(Lock.IsHeldByCurrentThread);

            pool.Return(node);
        }

        public void NotifyDisposed(ObjectDisposedException e, ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue, in TagList measurementTags)
        {
            Debug.Assert(Lock.IsHeldByCurrentThread);
            
            for (WaitNode? current = First, next; current is not null; current = next)
            {
                next = Unsafe.As<WaitNode>(current.Next);
                if (current.TrySetResult(new ManualResetCompletionSource.CustomCompletionData(Sentinel.Instance),
                        new Result<bool>(e),
                        out var resumable))
                {
                    Remove(current, in measurementTags);

                    if (resumable)
                        detachedQueue.Add(current);
                }
            }
            
            pool.Reset();
        }
    }
    
    private sealed class WaitNode : LinkedValueTaskCompletionSource<bool>
    {
        private AsyncParkingLot? owner;
        private object? userData;

        public void Initialize(AsyncParkingLot owner, int position, object? userData)
        {
            Debug.Assert(owner is not null);
            
            Position = position;
            this.userData = userData;
            this.owner = owner;
        }

        protected override void AfterConsumed()
            => owner?.ReturnNode(this);

        public int Position
        {
            get;
            private set;
        }

        public bool Matches(int position, object? userData)
            => Position == position && ReferenceEquals(this.userData, userData);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool NeedsRemoval => CompletionData is null;

        protected override Result<bool> GetTimeoutResult() => false;

        protected override void CleanUp()
        {
            owner = null;
            base.CleanUp();
        }
    }
}