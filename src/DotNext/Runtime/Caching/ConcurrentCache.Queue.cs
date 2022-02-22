using System.Diagnostics;

namespace DotNext.Runtime.Caching;

public partial class ConcurrentCache<TKey, TValue>
{
    private sealed class Command
    {
        private Func<KeyValuePair, KeyValuePair?>? invoker;
        private KeyValuePair? target;
        internal volatile Command? Next;

        internal void Initialize(Func<KeyValuePair, KeyValuePair?> invoker, KeyValuePair target)
        {
            this.invoker = invoker;
            this.target = target;
        }

        internal void Clear()
        {
            invoker = null;
            target = null;
        }

        internal KeyValuePair? Invoke()
        {
            Debug.Assert(target is not null);
            Debug.Assert(invoker is not null);

            return invoker.Invoke(target);
        }
    }

    private volatile bool rateLimitReached;
    private volatile Command commandQueueWritePosition;
    private Command commandQueueReadPosition;

    // Command pool fields
    private Command? pooledCommand; // volatile

    private Command RentCommand()
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

        current.Next = null;
        return current;
    }

    private void ReturnCommand(Command command)
    {
        // this method doesn't ensure that the command returned back to the pool
        // this assumption is needed to avoid spin-lock inside of the monitor lock
        command.Clear();
        var currentValue = command.Next = Volatile.Read(ref pooledCommand);
        Interlocked.CompareExchange(ref pooledCommand, command, currentValue);
    }

    private void EnqueueAndDrain(Func<KeyValuePair, KeyValuePair?> invoker, KeyValuePair target)
    {
        // enqueue
        Enqueue(invoker, target);

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

    private void Enqueue(Func<KeyValuePair, KeyValuePair?> invoker, KeyValuePair target)
    {
        var command = RentCommand();
        command.Initialize(invoker, target);
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
                if (command.Invoke() is KeyValuePair evictedPair && evictionHandler is not null)
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