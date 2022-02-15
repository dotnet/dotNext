using System.Runtime.InteropServices;

namespace DotNext.Runtime.Caching;

public partial class ConcurrentCache<TKey, TValue>
{
    private enum CommandType
    {
        Read = 0,
        Add,
        Remove,
    }

    private sealed class Command
    {
        internal readonly CommandType Type;
        internal readonly KeyValuePair Pair;
        internal volatile Command? Next;

        internal Command(CommandType type, KeyValuePair pair)
        {
            Type = type;
            Pair = pair;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private struct CommandQueueReader
    {
        private Command current;

        internal CommandQueueReader(Command current) => this.current = current;

        internal Command Read(Command position, out bool positionReached)
        {
            for (var spin = new SpinWait(); ; spin.SpinOnce())
            {
                var next = current.Next;

                if (next is null)
                    continue;

                positionReached = ReferenceEquals(next, position);
                return current = next;
            }
        }
    }

    private Command commandQueueWritePosition;

    private static Command Enqueue(ref Command commandQueueWritePosition, CommandType type, KeyValuePair pair)
    {
        var command = new Command(type, pair);
        return Interlocked.Exchange(ref commandQueueWritePosition, command).Next = command;
    }

    private void EnqueueCommandAndDrainQueue(CommandType type, KeyValuePair pair)
    {
        // enqueue command
        var command = Enqueue(ref commandQueueWritePosition, type, pair);

        // drain buffer using load balancing
        for (var spinCounter = 0; ;)
        {
            var deque = currentDeque;

            // spin deque
            Interlocked.CompareExchange(ref currentDeque, deque.Next, deque);

            if (Monitor.IsEntered(deque))
            {
                try
                {
                    deque.DrainCommandQueue(command, ref commandQueueWritePosition, evictionHandler);
                }
                finally
                {
                    Monitor.Exit(deque);
                }

                break;
            }
            else if (++spinCounter == concurrencyLevel)
            {
                spinCounter = 0;
                Thread.Yield();
            }
        }
    }
}