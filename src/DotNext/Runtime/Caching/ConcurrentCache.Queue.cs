using System.Diagnostics;

namespace DotNext.Runtime.Caching;

public partial class ConcurrentCache<TKey, TValue>
{
    private sealed class Command
    {
        private Func<KeyValuePair, KeyValuePair?>? invoker;
        private KeyValuePair? target;
        internal Command? Next;

        internal void Initialize(Func<KeyValuePair, KeyValuePair?> invoker, KeyValuePair target)
        {
            Debug.Assert(invoker is not null);
            Debug.Assert(target is not null);

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

            return target.Removed ? null : invoker.Invoke(target);
        }
    }

    private bool rateLimitReached;
    private Command commandQueueWritePosition, commandQueueReadPosition;
    private Command? pool;

    private Command RentCommand()
    {
        Command? current, next = Volatile.Read(ref pool);
        do
        {
            if (next is null)
            {
                current = new();
                break;
            }

            current = next;
        }
        while (!ReferenceEquals(next = Interlocked.CompareExchange(ref pool, current.Next, current), current));

        current.Next = null;
        return current;
    }

    private void ReturnCommand(Command command)
    {
        // this method doesn't ensure that the command returned back to the pool
        // this assumption is needed to avoid spin-lock inside of the monitor lock
        command.Clear();
        var currentValue = command.Next = pool;
        Interlocked.CompareExchange(ref pool, command, currentValue);
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
        var command = commandQueueReadPosition.Next;

        for (var readerCounter = 0; command is not null && readerCounter < concurrencyLevel; commandQueueReadPosition = command, command = command.Next, readerCounter++)
        {
            // interpret command
            if (command.Invoke() is { } evictedPair && evictionHandler is not null)
            {
                Debug.Assert(evictedPair.Next is null);

                AddToEvictionList(evictedPair, ref evictedHead, ref evictedTail);
            }

            // commandQueueReadPosition points to the previous command that can be returned to the pool
            ReturnCommand(commandQueueReadPosition);
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