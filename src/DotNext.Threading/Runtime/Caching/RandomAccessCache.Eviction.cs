using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace DotNext.Runtime.Caching;

using CompilerServices;

public partial class RandomAccessCache<TKey, TValue>
{
    // SIEVE core
    private readonly int maxCacheCapacity; // reused as contention threshold, if growable is true
    private int currentSize;
    private KeyValuePair? evictionHead, evictionTail, sieveHand;
    
    private CancelableValueTaskCompletionSource? completionSource;

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task DoEvictionAsync(CancelableValueTaskCompletionSource source)
    {
        while (!source.IsCanceled)
        {
            if (queueHead.NextInQueue is KeyValuePair newHead)
            {
                queueHead.NextInQueue = Sentinel.Instance;
                queueHead = newHead;
            }
            else if (await source.WaitAsync(queueHead).ConfigureAwait(false))
            {
                continue;
            }
            else
            {
                break;
            }

            Debug.Assert(queueHead is not FakeKeyValuePair);

            EvictOrInsert(queueHead);
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

    private sealed class CancelableValueTaskCompletionSource : IValueTaskSource<bool>, IThreadPoolWorkItem
    {
        private object? continuationState;
        
        // possible transitions:
        // null or non-stub => stub (canceled, completed)
        // null => non-stub
        // stub (completed) => stub (canceled)
        // non-stub or stub (completed) => null
        private volatile Action<object?>? continuation;
        private short version = short.MinValue;

        private void Complete()
        {
            // null or non-stub => stub (completed)
            Action<object?>? current, tmp = continuation;
            do
            {
                current = tmp;
                if (current.IsStub())
                    return;
            } while (!ReferenceEquals(tmp = Interlocked.CompareExchange(ref continuation, ValueTaskSourceHelpers.CompletedStub, current), current));

            current?.Invoke(continuationState);
        }

        private bool GetResult()
        {
            // null, non-stub, stub (completed) => stub (canceled)
            Action<object?>? current, tmp = continuation;
            do
            {
                current = tmp;
                if (ReferenceEquals(current, ValueTaskSourceHelpers.CanceledStub))
                    return false;
            } while (!ReferenceEquals(tmp = Interlocked.CompareExchange(ref continuation, null, current), current));

            return true;
        }

        void IThreadPoolWorkItem.Execute() => Complete();

        bool IValueTaskSource<bool>.GetResult(short token)
        {
            Debug.Assert(token == version);

            continuationState = null;
            version++;
            return GetResult();
        }

        ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token) => continuation is { } c && c.IsStub()
            ? ValueTaskSourceStatus.Succeeded
            : ValueTaskSourceStatus.Pending;

        void IValueTaskSource<bool>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            continuationState = state;
            
            // null => non-stub
            if (Interlocked.CompareExchange(ref this.continuation, continuation, null) is not null)
            {
                continuation.Invoke(state);
            }
        }

        internal ValueTask<bool> WaitAsync(KeyValuePair pair)
        {
            if (!pair.TryAttachNotificationHandler(this))
                Complete();

            return new(this, version);
        }

        internal bool IsCanceled => ReferenceEquals(continuation, ValueTaskSourceHelpers.CanceledStub);

        internal void Cancel()
        {
            if (Interlocked.Exchange(ref continuation, ValueTaskSourceHelpers.CanceledStub) is { } c && !c.IsStub())
            {
                c(continuationState);
                continuationState = null;
            }
        }
    }
}

file static class ValueTaskSourceHelpers
{
    internal static readonly Action<object?> CompletedStub = Stub;
    internal static readonly Action<object?> CanceledStub = Stub;

    private static void Stub(object? state) => Debug.Fail("Should never be called");
    
    internal static bool IsStub(this Action<object?>? continuation)
        => continuation?.Method == CompletedStub.Method;
}