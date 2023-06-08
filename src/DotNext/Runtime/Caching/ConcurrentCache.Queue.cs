using System.Diagnostics;

namespace DotNext.Runtime.Caching;

public partial class ConcurrentCache<TKey, TValue>
{
    private class Command
    {
        internal Command? Next;

        internal virtual KeyValuePair? Invoke(ConcurrentCache<TKey, TValue> cache)
        {
            Debug.Fail("Should not be called");
            return null;
        }
    }

    private abstract class CacheCommand : Command
    {
        private protected readonly KeyValuePair target;

        private protected CacheCommand(KeyValuePair target)
        {
            Debug.Assert(target is not null);

            this.target = target;
        }

        internal override abstract KeyValuePair? Invoke(ConcurrentCache<TKey, TValue> cache);
    }

    private sealed class AddOrReadLFUCommand : CacheCommand
    {
        internal AddOrReadLFUCommand(KeyValuePair target)
            : base(target)
        {
        }

        internal override KeyValuePair? Invoke(ConcurrentCache<TKey, TValue> cache) => target.State switch
        {
            KeyValuePairState.Created => cache.OnAdd(target),
            KeyValuePairState.Consumed => cache.OnReadLFU(target),
            _ => null,
        };
    }

    private sealed class AddOrReadLRUCommand : CacheCommand
    {
        internal AddOrReadLRUCommand(KeyValuePair target)
            : base(target)
        {
        }

        internal override KeyValuePair? Invoke(ConcurrentCache<TKey, TValue> cache) => target.State switch
        {
            KeyValuePairState.Created => cache.OnAdd(target),
            KeyValuePairState.Consumed => cache.OnReadLRU(target),
            _ => null,
        };
    }

    private sealed class RemoveCommand : CacheCommand
    {
        internal RemoveCommand(KeyValuePair target)
            : base(target)
        {
        }

        internal override KeyValuePair? Invoke(ConcurrentCache<TKey, TValue> cache)
            => target.State is not KeyValuePairState.Removed ? cache.OnRemove(target) : null;
    }

    private bool rateLimitReached;
    private Command commandQueueWritePosition, commandQueueReadPosition;

    private unsafe void EnqueueAndDrain(Command cmd)
    {
        // enqueue
        Interlocked.Exchange(ref commandQueueWritePosition, cmd).Next = cmd;

        // drain
        if (TryEnterEvictionLock())
        {
            KeyValuePair? evictedPair;

            try
            {
                evictedPair = DrainQueue();
            }
            finally
            {
                Monitor.Exit(evictionLock);
            }

            // invoke handlers out of the lock
            OnEviction(evictedPair);
        }
    }

    private bool TryEnterEvictionLock()
    {
        bool result;
        if (rateLimitReached)
        {
            Monitor.Enter(evictionLock);
            result = true;
        }
        else
        {
            result = Monitor.TryEnter(evictionLock);
        }

        return result;
    }

    private KeyValuePair? DrainQueue()
    {
        Debug.Assert(Monitor.IsEntered(evictionLock));

        KeyValuePair? evictedHead = null, evictedTail = null;
        var command = commandQueueReadPosition.Next;

        for (var readerCounter = 0; command is not null && readerCounter < concurrencyLevel; commandQueueReadPosition = command, command = command.Next, readerCounter++)
        {
            // interpret command
            if (command.Invoke(this) is { } evictedPair && evictionHandler is not null)
            {
                Debug.Assert(evictedPair.Next is null);

                AddToEvictionList(evictedPair, ref evictedHead, ref evictedTail);
            }

            commandQueueReadPosition.Next = null; // help GC
        }

        rateLimitReached = command is not null;
        return evictedHead;

        static void AddToEvictionList(KeyValuePair pair, ref KeyValuePair? head, ref KeyValuePair? tail)
        {
            if (head is null || tail is null)
            {
                head = tail = pair;
            }
            else
            {
                tail.Next = pair;
                tail = pair;
            }
        }
    }
}