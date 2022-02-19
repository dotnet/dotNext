using System.Diagnostics;

namespace DotNext.Runtime.Caching;

using static Threading.AtomicReference;

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
        internal CommandType Type;
        internal KeyValuePair? Target;
        internal volatile Command? Next;
    }

    // eviction deque fields
    private readonly object evictionLock = new();
    private volatile bool rateLimitReached;
    private volatile Command commandQueueWritePosition;
    private Command commandQueueReadPosition;

    // Command pool fields
    private Command? pooledCommand; // volatile

    private Command RentCommand(CommandType type, KeyValuePair target)
    {
        Command? current, next = Volatile.Read(ref pooledCommand);
        do
        {
            if (next is null)
            {
                current = new();
                break;
            }

            current = next;
        }
        while (!ReferenceEquals(next = Interlocked.CompareExchange(ref pooledCommand, current.Next, current), current));

        current.Type = type;
        current.Target = target;
        current.Next = null;
        return current;
    }

    private void ReturnCommand(Command command)
    {
        // this method doesn't ensure that the command returned back to the pool
        // this assumption is needed to avoid spin-lock inside of the monitor lock
        command.Target = null;
        var currentValue = command.Next = Volatile.Read(ref pooledCommand);
        Interlocked.CompareExchange(ref pooledCommand, command, currentValue);
    }

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
        var command = RentCommand(type, target);
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
                Debug.Assert(command.Target is not null);
                var evictedPair = Execute(command.Type, command.Target);

                if (evictedPair is not null && evictionHandler is not null)
                {
                    Debug.Assert(evictedPair.Next is null);

                    AddToEvictionList(evictedPair, ref evictedHead, ref evictedTail);
                }

                // commandQueueReadPosition points to the previous command that can be returned to the pool
                ReturnCommand(commandQueueReadPosition);
            }
            else
            {
                rateLimitReached = true;
                break;
            }
        }

        this.rateLimitReached = rateLimitReached;
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