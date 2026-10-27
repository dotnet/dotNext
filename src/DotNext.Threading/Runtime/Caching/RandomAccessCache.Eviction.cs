using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.Caching;

using CompilerServices;
using Threading.Tasks;

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
        while (await queue.WaitAsync(source).ConfigureAwait(false))
        {
            ProcessQueue();
        }
    }

    private void ProcessQueue()
    {
        while (queue.TryDequeue() is { } item)
        {
            switch (item)
            {
                case KeyValuePair pair:
                    EvictOrInsert(pair);
                    break;
                case InvalidationSignal signal:
                    Invalidate(signal);
                    break;
                default:
                    Debug.Fail($"Unexpected command {item}");
                    break;
            }
        }
    }

    private void EvictOrInsert(KeyValuePair promoted)
    {
        // The pair can be removed concurrently. The reference acquired by Promote keeps the value alive
        // and guarantees that OnRemoved is called after OnAdded
        if (Evict(promoted))
        {
            promoted.Prepend(ref evictionHead, ref evictionTail);
            sieveHand ??= evictionTail;
            currentSize++;
            OnAdded(promoted);

            if (!promoted.ReleaseCounter())
                OnRemoved(promoted);
        }
        else
        {
            // the cache doesn't have enough resources (pair has too high weight), release the enqueued pair.
            // The weight is not added, so use Clear instead of OnRemoved
            if (promoted.MarkAsEvicted())
                promoted.ReleaseCounter(); // the reference owned by the cache, cannot reach zero

            if (!promoted.ReleaseCounter())
                Clear(promoted);

            promoted.Added();
        }
    }

    private void Invalidate(InvalidationSignal signal)
    {
        if (signal.Target is { } removed)
        {
            Invalidate(removed);
            signal.Target = null; // help GC, the signal remains the head of the queue
        }
        else
        {
            InvalidateAll();
        }
    }

    private void Invalidate(KeyValuePair removed)
    {
        // the pair can be detached by the SIEVE hand already
        if (removed.IsInEvictionList(evictionHead))
        {
            var previous = removed.DetachAndMoveBackward(ref evictionHead, ref evictionTail);
            if (ReferenceEquals(removed, sieveHand))
                sieveHand = previous ?? evictionTail;

            currentSize--;
        }
    }

    private void InvalidateAll()
    {
        // All the pairs in the list are dead, because the invalidation holds all the bucket locks.
        // The pairs inserted after the invalidation are enqueued after the signal
        for (KeyValuePair? current = evictionHead, next; current is not null; current = next)
        {
            next = current.MoveForward();
            current.ClearSieveLinks();
        }

        evictionHead = evictionTail = sieveHand = null;
        currentSize = 0;
    }

    private bool Evict(KeyValuePair promoted)
    {
        while (IsEvictionRequired(promoted))
        {
            if (sieveHand is null)
                return false;

            if (!sieveHand.TryEvict(out var removed))
            {
                sieveHand = sieveHand.MoveBackward() ?? evictionTail;
            }
            else
            {
                var removedPair = sieveHand;
                sieveHand = sieveHand.DetachAndMoveBackward(ref evictionHead, ref evictionTail) ?? evictionTail;
                currentSize--;
                if (!removed && !removedPair.ReleaseCounter())
                {
                    OnRemoved(removedPair);
                    buckets.CleanUp(parkingLot, removedPair.KeyHashCode);
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
        => promoted.Added();

    private protected virtual void OnRemoved(KeyValuePair demoted)
        => Clear(demoted);

    private void Clear(KeyValuePair demoted)
    {
        Eviction?.Invoke(demoted.Key, GetValue(demoted));
        ClearValue(demoted);
    }

    private void RebuildEvictionState(BucketList buckets)
    {
        if (keyComparer is { } keyComparerCopy)
        {
            RebuildEvictionState<EqualityComparerSelector, EqualityComparerSelectorFactory>(new(keyComparerCopy), buckets);
        }
        else
        {
            RebuildEvictionState<DefaultSelector, DefaultSelectorFactory>(new(), buckets);
        }
    }

    private void RebuildEvictionState<TPredicate, TFactory>(TFactory factory, BucketList buckets)
        where TPredicate : struct, IEquatable<TKey>
        where TFactory : struct, ISupplier<TKey, TPredicate>
    {
        KeyValuePair? newHead = null, newTail = null;
        var newSize = 0;

        // dead pairs are skipped, because the new bucket list doesn't contain them
        for (KeyValuePair? current = evictionTail, previous; current is not null; current = previous)
        {
            previous = current.MoveBackward();
            if (buckets.FindPair(factory.Invoke(current.Key), current.KeyHashCode) is { } pair)
            {
                pair.Prepend(ref newHead, ref newTail);
                newSize++;
            }

            // the old pair is no longer in the eviction list
            current.ClearSieveLinks();
        }

        evictionHead = newHead;
        evictionTail = newTail;
        currentSize = newSize;
        sieveHand = sieveHand is not null ? buckets.FindPair(factory.Invoke(sieveHand.Key), sieveHand.KeyHashCode) ?? newTail : null;
    }

    internal partial class KeyValuePair
    {
        private (KeyValuePair? Previous, KeyValuePair? Next) sieveLinks;
        private SieveState sieveState;

        // null, or Task, or AddedEventSource
        private volatile object? addedEvent;

        internal KeyValuePair? MoveBackward()
            => sieveLinks.Previous;

        internal KeyValuePair? MoveForward()
            => sieveLinks.Next;

        internal void ClearSieveLinks() => sieveLinks = default;

        // can be called by the eviction thread only
        internal bool IsInEvictionList(KeyValuePair? head)
            => sieveLinks.Previous is not null || sieveLinks.Next is not null || ReferenceEquals(this, head);

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
                previous?.sieveLinks.Next = next;

                next?.sieveLinks.Previous = previous;
            }
        }

        internal bool TryEvict(out bool removed) => sieveState.TryEvict(out removed);

        internal bool Visit() => sieveState.Visit();

        internal bool MarkAsEvicted() => sieveState.MarkAsEvicted();

        internal bool MarkAsDead()
            => MarkAsEvicted() && !ReleaseCounter();

        internal bool IsDead => sieveState.IsDead;
        
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

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal Task AddedEvent
        {
            get
            {
                var result = Task.CompletedTask;
                for (object? addedEventCopy = addedEvent; !IsDead;)
                {
                    switch (addedEventCopy)
                    {
                        case AddedEventSource source:
                            result = source.Task;
                            break;
                        case Task task:
                            result = task;
                            break;
                        default:
                            var src = new AddedEventSource();
                            addedEventCopy = Interlocked.CompareExchange(ref addedEvent, src, comparand: null) ?? src;
                            continue;
                    }

                    break;
                }

                return result;
            }
        }

        internal void Added()
        {
            if (Interlocked.CompareExchange(ref addedEvent, Task.CompletedTask, comparand: null) is AddedEventSource source)
                source.TrySetResult();
        }
    }
}

file sealed class AddedEventSource() : TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);