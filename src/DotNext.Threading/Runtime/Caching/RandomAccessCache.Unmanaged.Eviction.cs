using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.Caching;

using Collections.Concurrent;
using CompilerServices;
using Threading;

partial class RandomAccessCache<TValue> : Epoch.INativeResourceManager
{
    private readonly Epoch epoch;
    [SuppressMessage("Usage", "CA2213", Justification = "The signal can be set by the sessions after disposal")]
    private readonly AsyncAutoResetEvent signal = new(initialState: false);

    // Promotion queue: multiple producers, the eviction thread is the single consumer.
    // The commands are managed objects, because they are consumed as soon as possible
    private MpscQueue queue = new();

    // SIEVE core, owned by the eviction thread
    private unsafe Entry* evictionHead, evictionTail, sieveHand;
    private int currentSize;

    // the retired entries are reclaimed immediately after the teardown
    private volatile bool tornDown;

    private volatile TaskCompletionSource? batchCompleted;

    // the caller holds the bucket lock and passes the reference to the entry to the command
    private unsafe void Enqueue(Entry* entry)
    {
        queue.Enqueue(new Promotion(entry));
        signal.Set();
    }

    // The caller holds the bucket lock and a reference to the removed entry, or all the bucket locks if the entry is null.
    // The signal passes through the promotion queue, so it is processed after the promotion of the removed entry
    private unsafe void NotifyRemoved(Entry* removed)
    {
        removed->AddReference(); // the reference owned by the signal
        NotifyRemovedCore(removed);
    }

    private unsafe void NotifyRemovedAll() => NotifyRemovedCore(removed: null);

    private unsafe void NotifyRemovedCore(Entry* removed)
    {
        queue.Enqueue(new InvalidationSignal { Target = removed });
        signal.Set();
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task DoEvictionAsync()
    {
        while (!IsDisposingOrDisposed)
        {
            await signal.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            ProcessQueue();
            CompleteBatch();

            // Reclaims the retired entries if the readers left the epoch. Otherwise, the entries
            // are reclaimed on the next wake up
            epoch.TryClear();
        }

        await TearDownAsync().ConfigureAwait(false);
    }

    private void ProcessQueue()
    {
        while (queue.TryDequeue() is { } command)
        {
            switch (command)
            {
                case Promotion promotion:
                    Promote(promotion);
                    break;
                case InvalidationSignal invalidation:
                    Invalidate(invalidation);
                    break;
                default:
                    Debug.Fail($"Unexpected command {command}");
                    break;
            }
        }
    }

    private unsafe void Promote(Promotion promotion)
    {
        var entry = promotion.Entry;

        if (entry->IsDead)
        {
            // removed before promotion
            entry->IsProcessed = true;
            Release(entry);
        }
        else
        {
            // the reference owned by the command is passed to the SIEVE list
            AddToEvictionList(entry);
            entry->IsProcessed = true;
        }
    }

    private unsafe void Invalidate(InvalidationSignal invalidation)
    {
        var removed = invalidation.Target;
        if (removed is null)
        {
            ClearEvictionList();
        }
        else
        {
            Invalidate(removed);
        }
    }

    private unsafe void Invalidate(Entry* removed)
    {
        // the entry can be detached by the SIEVE hand already, or skipped as dead before the promotion
        if (removed->SievePrevious is not null || removed->SieveNext is not null || removed == evictionHead)
        {
            var previous = Detach(removed);
            if (removed == sieveHand)
                sieveHand = previous;

            currentSize--;
            Release(removed); // the reference owned by the SIEVE list
        }

        Release(removed); // the reference owned by the signal
    }

    // Called on invalidation of the entire cache or on teardown. In case of invalidation, all the entries in the list
    // are dead, because the invalidation holds all the bucket locks. The entries inserted after the invalidation
    // are enqueued after the signal
    private unsafe void ClearEvictionList()
    {
        for (Entry* entry = evictionHead, next; entry is not null; entry = next)
        {
            next = entry->SieveNext;
            entry->SievePrevious = entry->SieveNext = null;
            Release(entry); // the reference owned by the SIEVE list
        }

        evictionHead = evictionTail = sieveHand = null;
        currentSize = 0;
    }

    private unsafe void AddToEvictionList(Entry* entry)
    {
        Evict();

        entry->SievePrevious = null;
        entry->SieveNext = evictionHead;
        if (evictionHead is null)
        {
            evictionTail = entry;
        }
        else
        {
            evictionHead->SievePrevious = entry;
        }

        evictionHead = entry;
        if (sieveHand is null)
            sieveHand = evictionTail;

        currentSize++;
    }

    private unsafe void Evict()
    {
        while (currentSize >= Capacity && sieveHand is not null)
        {
            var entry = sieveHand;
            if (entry->Unvisit())
            {
                sieveHand = entry->SievePrevious is not null ? entry->SievePrevious : evictionTail;
                continue;
            }

            sieveHand = Detach(entry);
            currentSize--;

            // the entry is still linked to its bucket if it was alive
            if (entry->MarkAsEvicted())
                TryCleanUpBucket(entry->KeyHashCode);

            Release(entry); // the reference owned by the SIEVE list
        }
    }

    private unsafe Entry* Detach(Entry* entry)
    {
        var previous = entry->SievePrevious;
        var next = entry->SieveNext;

        if (previous is null)
        {
            evictionHead = next;
        }
        else
        {
            previous->SieveNext = next;
        }

        if (next is null)
        {
            evictionTail = previous;
        }
        else
        {
            next->SievePrevious = previous;
        }

        entry->SievePrevious = entry->SieveNext = null;
        return previous is not null ? previous : evictionTail;
    }

    // the entry has no owners, but the readers can still traverse it
    private unsafe void Retire(Entry* entry)
    {
        try
        {
            Eviction?.Invoke(entry->Key, entry->Value);
        }
        finally
        {
            Reclaim(entry);
        }
    }

    private unsafe void Reclaim(Entry* entry)
    {
        bool deferred;

        // the scope prevents the teardown from missing the entry deferred concurrently with it
        using (var scope = epoch.Enter())
        {
            if (tornDown)
            {
                deferred = false;
                
                // no readers after the teardown
                Free(entry);
            }
            else
            {
                deferred = true;
                scope.Defer((nuint)entry);
            }
        }

        // wake up the eviction thread to reclaim the retired entries
        if (deferred)
            signal.Set();
    }

    private async Task TearDownAsync()
    {
        // wait for the writers holding the bucket locks, the new writers are canceled by the lifetime token
        for (var i = 0; i < Capacity; i++)
        {
            await AcquireBucketAsync(i, CancellationToken.None).ConfigureAwait(false);
        }

        TearDown();
        CompleteBatch();

        // reclaim the retired entries and wait for the readers to leave the epoch
        await epoch.ClearAsync(CancellationToken.None).ConfigureAwait(false);

        // the remaining entries are reclaimed by the sessions immediately
        tornDown = true;

        // reclaim the entries deferred by the sessions concurrently with the flag
        await epoch.ClearAsync(CancellationToken.None).ConfigureAwait(false);

        // the readers that could see the bucket array have left
        unsafe
        {
            Free(buckets);
        }

        parkingLot.Dispose();
        Cancel();
    }

    private unsafe void TearDown()
    {
        // the queue is drained without promotion
        while (queue.TryDequeue() is { } command)
        {
            switch (command)
            {
                case Promotion promotion:
                    var entry = promotion.Entry;
                    entry->IsProcessed = true;
                    Release(entry);
                    break;
                case InvalidationSignal invalidation:
                    if (invalidation.Target is not null)
                        Release(invalidation.Target);

                    invalidation.Target = null;
                    break;
            }
        }

        ClearEvictionList();

        for (var i = 0; i < Capacity; i++)
        {
            var bucket = this[i];
            var entry = bucket->First;
            Volatile.Write(&bucket->First, null);

            // the readers can still traverse the chain, so the links remain intact
            for (Entry* next; entry is not null; entry = next)
            {
                next = entry->NextInBucket;
                entry->MarkAsEvicted();
                Release(entry);
            }
        }
    }

    // Called by the finalizer. The cache is unreachable, so no other thread can access it: the eviction loop
    // is suspended, and there are no sessions. The reference counters are not reliable, because the abandoned
    // sessions and promotion waiters never release their references. Thus, every reachable entry is released once
    private unsafe void ReleaseAbandonedMemory()
    {
        var entries = new HashSet<nint>();

        while (queue.TryDequeue() is { } command)
        {
            switch (command)
            {
                case Promotion promotion:
                    entries.Add((nint)promotion.Entry);
                    break;
                case InvalidationSignal invalidation when invalidation.Target is not null:
                    entries.Add((nint)invalidation.Target);
                    break;
            }
        }

        for (var entry = evictionHead; entry is not null; entry = entry->SieveNext)
        {
            entries.Add((nint)entry);
        }

        for (var i = 0; i < Capacity; i++)
        {
            for (var entry = this[i]->First; entry is not null; entry = entry->NextInBucket)
            {
                entries.Add((nint)entry);
            }
        }

        foreach (var entry in entries)
        {
            Free((Entry*)entry);
        }

        // the retired entries are not linked to the structures above, and no thread is in the epoch
        var cleared = epoch.TryClear();
        Debug.Assert(cleared);
        
        Free(buckets);
    }
    
    unsafe ref nuint Epoch.INativeResourceManager.GetNextHandle(nuint handle)
    {
        // reinterpret the field itself, not the entry it points to
        var entry = (Entry*)handle;
        return ref Unsafe.AsRef<nuint>(&entry->SieveNext);
    }

    unsafe void Epoch.INativeResourceManager.Release(nuint handle)
        => Free((Entry*)handle);

    private Task GetBatchTask()
    {
        if (batchCompleted is not { } source)
        {
            var newSource = new BatchCompletedNotification();
            source = Interlocked.CompareExchange(ref batchCompleted, newSource, null) ?? newSource;
        }

        return source.Task;
    }

    private void CompleteBatch() => Interlocked.Exchange(ref batchCompleted, null)?.TrySetResult();

    // the caller owns a reference to the entry
    private async ValueTask WaitForPromotionAsync(nint entry)
    {
        try
        {
            while (!IsProcessed(entry))
            {
                // capture the task before checking the flag to avoid the lost wakeup
                var task = GetBatchTask();
                if (IsProcessed(entry))
                    break;

                await task.ConfigureAwait(false);
            }
        }
        finally
        {
            unsafe
            {
                Release((Entry*)entry);
            }
        }

        static unsafe bool IsProcessed(nint entry) => ((Entry*)entry)->IsProcessed;
    }

    private sealed unsafe class Promotion(Entry* entry) : MpscQueue.Item
    {
        internal readonly Entry* Entry = entry;

        public override string ToString() => "Promotion";
    }

    // Notifies the eviction thread that the entry is removed from the cache, or all the entries if Target is null
    private sealed unsafe class InvalidationSignal : MpscQueue.Item
    {
        internal required Entry* Target;

        public override string ToString() => Target is null ? "Invalidate all" : "Invalidation";
    }
}

file sealed class BatchCompletedNotification() : TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);