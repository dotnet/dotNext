using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

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

    /// <summary>
    /// Represents a command to be applied to the deque.
    /// </summary>
    [DebuggerDisplay($"{{{nameof(Type)}}} {{{nameof(Pair)}}}")]
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

    /// <summary>
    /// Represents read position within the queue of commands.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay($"Pending commands = {{{nameof(Count)}}}")]
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

        [ExcludeFromCodeCoverage]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int Count
        {
            get
            {
                var result = 0;
                for (var current = this.current.Next; current is not null; current = current.Next)
                    result++;

                return result;
            }
        }
    }

    /// <summary>
    /// Last written command in the queue.
    /// </summary>
    private Command commandQueueWritePosition;

    /// <summary>
    /// Enqueues a new command to the queue.
    /// </summary>
    /// <param name="commandQueueWritePosition">The location of the queue head.</param>
    /// <param name="type">The command type.</param>
    /// <param name="pair">The command argument.</param>
    /// <returns>The enqueued command.</returns>
    private static Command Enqueue(ref Command commandQueueWritePosition, CommandType type, KeyValuePair pair)
    {
        var command = new Command(type, pair);
        return Interlocked.Exchange(ref commandQueueWritePosition, command).Next = command;
    }

    /// <summary>
    /// Enqueues a new command to the queue and drains the queue for the selected deque.
    /// </summary>
    /// <param name="type">The command type.</param>
    /// <param name="pair">The command argument.</param>
    private void EnqueueCommandAndDrainQueue(CommandType type, KeyValuePair pair)
    {
        // enqueue command
        var command = Enqueue(ref commandQueueWritePosition, type, pair);

        // drain buffer using load balancing
        for (var spinCounter = 0; ; spinCounter++)
        {
            var deque = currentDeque;

            // spin deque
            Interlocked.CompareExchange(ref currentDeque, deque.Next, deque);

            if (Monitor.TryEnter(deque))
                goto drain_command_queue;

            if (spinCounter < concurrencyLevel)
                continue;

            // concurrencyLevel is less than the number of concurrent threads
            Monitor.Enter(deque);

        drain_command_queue:
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
    }
}