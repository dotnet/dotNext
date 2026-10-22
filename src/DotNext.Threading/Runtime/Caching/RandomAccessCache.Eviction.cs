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
        while (await source.WaitAsync(queueHead).ConfigureAwait(false))
        {
            ProcessQueue();
        }
    }

    private void ProcessQueue()
    {
        while (queueHead.NextInQueue is KeyValuePair newHead)
        {
            queueHead.NextInQueue = Sentinel.Instance;
            queueHead = newHead;
                
            Debug.Assert(newHead is not FakeKeyValuePair);
            EvictOrInsert(newHead);
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

    private bool Evict(KeyValuePair promoted)
    {
        while (IsEvictionRequired(promoted))
        {
            if (sieveHand is null)
                return false;

            if (!sieveHand.Evict(out var removed))
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
        for (var current = evictionTail; current is not null; current = current.MoveBackward())
        {
            if (buckets.FindPair(factory.Invoke(current.Key), current.KeyHashCode) is { } pair)
            {
                pair.Prepend(ref newHead, ref newTail);
                newSize++;
            }
        }

        evictionHead = newHead;
        evictionTail = newTail;
        currentSize = newSize;
        sieveHand = sieveHand is not null ? buckets.FindPair(factory.Invoke(sieveHand.Key), sieveHand.KeyHashCode) ?? newTail : null;
    }

    internal partial class KeyValuePair
    {
        private const int EvictedState = -1;
        private const int NotVisitedState = 0;
        private const int VisitedState = 1;

        private (KeyValuePair? Previous, KeyValuePair? Next) sieveLinks;
        private volatile int cacheState;

        // null, or Task, or AddedEventSource
        private volatile object? addedEvent;

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
                previous?.sieveLinks.Next = next;

                next?.sieveLinks.Previous = previous;
            }
        }

        internal bool Evict(out bool removed)
        {
            var counter = Interlocked.Decrement(ref cacheState);
            removed = counter < EvictedState;
            return counter < NotVisitedState;
        }

        internal bool Visit() => cacheState switch
        {
            VisitedState => true,
            NotVisitedState => Interlocked.CompareExchange(ref cacheState, VisitedState, NotVisitedState) >= NotVisitedState,
            _ => false,
        };

        internal bool MarkAsEvicted()
            => Interlocked.Exchange(ref cacheState, EvictedState) >= NotVisitedState;

        internal bool MarkAsDead()
            => MarkAsEvicted() && !ReleaseCounter();

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

        private sealed class AddedEventSource() : TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
                if (current.IsStub)
                    return;
            } while (!ReferenceEquals(tmp = Interlocked.CompareExchange(ref continuation, ValueTaskSourceHelpers.CompletedStub, current), current));

            current?.Invoke(continuationState);
        }

        private bool TryReset()
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
            return TryReset();
        }

        ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token) => continuation is { IsStub: false }
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

        internal void Cancel()
        {
            if (Interlocked.Exchange(ref continuation, ValueTaskSourceHelpers.CanceledStub) is { IsStub: false } c)
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

    extension(Action<object?>? continuation)
    {
        public bool IsStub => continuation?.Method == CompletedStub.Method;
    }
}