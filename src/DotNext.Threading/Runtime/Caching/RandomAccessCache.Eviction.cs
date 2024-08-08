using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static System.Threading.Timeout;

namespace DotNext.Runtime.Caching;

using CompilerServices;

public partial class RandomAccessCache<TKey, TValue>
{
    // SIEVE core
    private readonly int maxCacheSize;
    private int currentSize;
    private KeyValuePair? evictionHead, evictionTail, sieveHand;

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task DoEvictionAsync()
    {
        var cancellationTask = Task.Delay(InfiniteTimeSpan, lifetimeToken);
        while (!cancellationTask.IsCompleted)
        {
            KeyValuePair dequeued;
            
            // inlined to remove allocation of async state machine on every call
            for (dequeued = Volatile.Read(in promotionHead); !dequeued.TryConsumePromotion();)
            {
                if (dequeued.NextPromotion is { } newHead)
                {
                    var tmp = Interlocked.CompareExchange(ref promotionHead, newHead, dequeued);
                    dequeued = ReferenceEquals(tmp, dequeued) ? newHead : tmp;
                }
                else
                {
                    var resultTask = await Task.WhenAny(dequeued.Task, cancellationTask).ConfigureAwait(false);
                    if (ReferenceEquals(resultTask, cancellationTask))
                        return;
                }
            }

            switch (dequeued)
            {
                case FakeKeyValuePair:
                    break;
                default:
                    EvictOrInsert(dequeued);
                    break;
            }
        }
    }

    private void EvictOrInsert(KeyValuePair dequeued)
    {
        if (currentSize == maxCacheSize)
            Evict();

        Debug.Assert(currentSize < maxCacheSize);
        dequeued.Prepend(ref evictionHead, ref evictionTail);
        sieveHand ??= evictionTail;
        currentSize++;
    }

    private void Evict()
    {
        Debug.Assert(sieveHand is not null);
        Debug.Assert(evictionHead is not null);
        Debug.Assert(evictionTail is not null);

        while (sieveHand is not null)
        {
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
                    OnEviction?.Invoke(removedPair.Key, GetValue(removedPair));
                    ClearValue(removedPair);
                    TryCleanUpBucket(GetBucket(removedPair.KeyHashCode));
                    break;
                }
            }
        }
    }

    private void TryCleanUpBucket(Bucket bucket)
    {
        if (bucket.TryAcquire())
        {
            try
            {
                bucket.CleanUp(keyComparer);
            }
            finally
            {
                bucket.Release();
            }
        }
    }
    
    partial class KeyValuePair
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
}