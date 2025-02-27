using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;
using DotNext.Threading;

namespace DotNext.Runtime.Caching;

using CompilerServices;

public partial class RandomAccessCache<TKey, TValue>
{
    private readonly CancelableValueTaskCompletionSource completionSource;
    
    // SIEVE core
    private readonly int maxCacheCapacity; // reused as contention threshold, if growable is true
    private int currentSize;
    private KeyValuePair? evictionHead, evictionTail, sieveHand;

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task DoEvictionAsync()
    {
        while (!IsDisposingOrDisposed)
        {
            if (queueHead.NextInQueue is KeyValuePair newHead)
            {
                queueHead.NextInQueue = Sentinel.Instance;
                queueHead = newHead;
            }
            else if (await completionSource.WaitAsync(queueHead).ConfigureAwait(false))
            {
                continue;
            }
            else
            {
                break;
            }

            Debug.Assert(queueHead is not FakeKeyValuePair);

            switch (queueHead)
            {
                case TerminateCommand:
                    return;
                case FakeKeyValuePair:
                    Debug.Fail("Unexpected command");
                    break;
                default:
                    EvictOrInsert(queueHead);
                    break;
            }
        }
    }

    private void EvictOrInsert(KeyValuePair promoted)
    {
        if (Evict(promoted))
        {
            promoted.Prepend(ref evictionHead, ref evictionTail);
            sieveHand ??= evictionTail;
            currentSize++;
            OnAdded(promoted);
        }
    }

    private bool Evict(KeyValuePair promoted)
    {
        while (IsEvictionRequired(promoted))
        {
            if (sieveHand is null)
            {
                // if eviction still required, we need to release the enqueued key/value pair because the current cache
                // doesn't have enough resources
                Clear(promoted);
                return false;
            }
            
            if (!sieveHand.Evict(out var removed))
            {
                sieveHand = sieveHand.MoveBackward() ?? evictionTail;
            }
            else
            {
                var removedPair = sieveHand;
                sieveHand = sieveHand.DetachAndMoveBackward(ref evictionHead, ref evictionTail) ?? evictionTail;
                currentSize--;
                if (!removed && removedPair.ReleaseCounter() is false)
                {
                    OnRemoved(removedPair);
                    TryCleanUpBucket(ref buckets.GetByHash(removedPair.KeyHashCode));
                }
            }
        }

        return true;
    }
    
    // cannot be called concurrently, doesn't require synchronization
    private protected virtual bool IsEvictionRequired(KeyValuePair promoted)
        => currentSize == maxCacheCapacity;

    // cannot be called concurrently, doesn't require synchronization
    private protected virtual void OnAdded(KeyValuePair promoted)
    {
    }

    private protected virtual void OnRemoved(KeyValuePair demoted)
        => Clear(demoted);

    private void Clear(KeyValuePair demoted)
    {
        Eviction?.Invoke(demoted.Key, GetValue(demoted));
        ClearValue(demoted);
    }

    private void TryCleanUpBucket(ref Bucket bucket)
    {
        var bucketsCopy = buckets;
        if (bucket.Lock.TryAcquire())
        {
            try
            {
                if (ReferenceEquals(bucketsCopy, buckets))
                    bucket.CleanUp();
            }
            finally
            {
                bucket.Lock.Release();
            }
        }
    }

    private void RebuildEvictionState(BucketList buckets)
    {
        KeyValuePair? newHead = null, newTail = null;
        for (var current = evictionTail; current is { IsDead: false }; current = current.MoveBackward())
        {
            buckets.FindPair(keyComparer, current.Key, current.KeyHashCode)?.Prepend(ref newHead, ref newTail);
        }

        evictionHead = newHead;
        evictionTail = newTail;
        sieveHand = sieveHand is not null ? buckets.FindPair(keyComparer, sieveHand.Key, sieveHand.KeyHashCode) ?? newTail : null;
    }

    internal partial class KeyValuePair
    {
        private const int EvictedState = -1;
        private const int NotVisitedState = 0;
        private const int VisitedState = 1;

        private (KeyValuePair? Previous, KeyValuePair? Next) sieveLinks;
        private volatile int cacheState;

        internal KeyValuePair? MoveBackward()
            => sieveLinks.Previous;

        internal void Prepend([NotNull] ref KeyValuePair? head, [NotNull] ref KeyValuePair? tail)
        {
            if (head is null || tail is null)
            {
                head = tail = this;
            }
            else
            {
                head = (sieveLinks.Next = head).sieveLinks.Previous = this;
            }
        }

        internal KeyValuePair? DetachAndMoveBackward(ref KeyValuePair? head, ref KeyValuePair? tail)
        {
            var (previous, next) = sieveLinks;

            if (previous is null)
            {
                head = next;
            }

            if (next is null)
            {
                tail = previous;
            }

            MakeLink(previous, next);
            sieveLinks = default;
            return previous;

            static void MakeLink(KeyValuePair? previous, KeyValuePair? next)
            {
                if (previous is not null)
                {
                    previous.sieveLinks.Next = next;
                }

                if (next is not null)
                {
                    next.sieveLinks.Previous = previous;
                }
            }
        }

        internal bool Evict(out bool removed)
        {
            var counter = Interlocked.Decrement(ref cacheState);
            removed = counter < EvictedState;
            return counter < NotVisitedState;
        }

        internal bool Visit()
            => Interlocked.CompareExchange(ref cacheState, VisitedState, NotVisitedState) >= NotVisitedState;

        internal bool MarkAsEvicted()
            => Interlocked.Exchange(ref cacheState, EvictedState) >= NotVisitedState;

        internal bool IsDead => cacheState < NotVisitedState;
        
        [ExcludeFromCodeCoverage]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal (int Alive, int Dead) EvictionNodesCount
        {
            get
            {
                var alive = 0;
                var dead = 0;
                for (var current = this; current is not null; current = current.sieveLinks.Next)
                {
                    ref var counterRef = ref current.IsDead ? ref dead : ref alive;
                    counterRef++;
                }

                return (alive, dead);
            }
        }
    }

    private sealed class CancelableValueTaskCompletionSource : Disposable, IValueTaskSource<bool>, IThreadPoolWorkItem
    {
        private object? continuationState;
        private volatile Action<object?>? continuation;
        private short version = short.MinValue;

        private void MoveTo(Action<object?> stub)
        {
            Debug.Assert(ValueTaskSourceHelpers.IsStub(stub));

            // null, non-stub => stub
            Action<object?>? current, tmp = continuation;

            do
            {
                current = tmp;
                if (current is not null && ValueTaskSourceHelpers.IsStub(current))
                    return;
            } while (!ReferenceEquals(tmp = Interlocked.CompareExchange(ref continuation, stub, current), current));

            current?.Invoke(continuationState);
        }

        void IThreadPoolWorkItem.Execute() => MoveTo(ValueTaskSourceHelpers.CompletedStub);

        bool IValueTaskSource<bool>.GetResult(short token)
        {
            Debug.Assert(token == version);

            continuationState = null;
            if (IsDisposingOrDisposed)
                return false;

            Reset();
            return true;
        }

        private void Reset()
        {
            version++;
            continuationState = null;
            continuation = null;
        }

        ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token)
        {
            return continuation is { } c && ValueTaskSourceHelpers.IsStub(c) || IsDisposingOrDisposed
                ? ValueTaskSourceStatus.Succeeded
                : ValueTaskSourceStatus.Pending;
        }

        void IValueTaskSource<bool>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            continuationState = state;
            if (Interlocked.CompareExchange(ref this.continuation, continuation, null) is not null)
            {
                continuation.Invoke(state);
            }
        }

        internal ValueTask<bool> WaitAsync(KeyValuePair pair)
        {
            if (!pair.TryAttachNotificationHandler(this))
                continuation = ValueTaskSourceHelpers.CompletedStub;

            return new(this, version);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                MoveTo(ValueTaskSourceHelpers.CanceledStub);
            }

            base.Dispose(disposing);
        }
    }
}

file static class ValueTaskSourceHelpers
{
    internal static readonly Action<object?> CompletedStub = Stub;
    internal static readonly Action<object?> CanceledStub = Stub;
    
    private static void Stub(object? state)
    {
    }
    
    internal static bool IsStub(Action<object?> continuation)
        => continuation.Method == CompletedStub.Method;
}