using System.Diagnostics;

namespace DotNext.Runtime.Caching;

public partial class ConcurrentCache<TKey, TValue>
{
    /// <summary>
    /// Represents a set of actions that can be applied to the deque.
    /// </summary>
    private enum CommandType : byte
    {
        Read = 0,
        Add,
        Remove,
    }

    private sealed class Command
    {
        internal readonly CommandType Type;
        internal readonly KeyValuePair Target;
        internal volatile Command? Next;

        internal Command(CommandType type, KeyValuePair target)
        {
            Type = type;
            Target = target;
        }
    }

    private readonly object evictionLock = new();
    private volatile bool rateLimitReached;
    private volatile Command commandQueueWritePosition;
    private Command commandQueueReadPosition;

    private void EnqueueAndDrain(CommandType type, KeyValuePair target)
    {
        // enqueue
        Enqueue(type, target);

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
            if (evictedPair is not null)
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

    private void Enqueue(CommandType type, KeyValuePair target)
    {
        var command = new Command(type, target);
        Interlocked.Exchange(ref commandQueueWritePosition, command).Next = command;
    }

    private KeyValuePair? DrainQueue()
    {
        Debug.Assert(Monitor.IsEntered(evictionLock));

        KeyValuePair? evictedHead = null, evictedTail = null;
        var rateLimitReached = false;
        var command = commandQueueReadPosition.Next;
        for (var readerCounter = 0; command is not null; commandQueueReadPosition = command, command = command.Next, readerCounter++)
        {
            if (readerCounter < concurrencyLevel)
            {
                // interpret command
                var evictedPair = Execute(command.Type, command.Target);

                if (evictedHead is null || evictedTail is null)
                {
                    evictedHead = evictedTail = evictedPair;
                }
                else
                {
                    evictedTail.Next = evictedPair;
                    evictedTail = evictedPair;
                }
            }
            else
            {
                rateLimitReached = true;
                break;
            }
        }

        this.rateLimitReached = rateLimitReached;
        return evictedHead;
    }
}