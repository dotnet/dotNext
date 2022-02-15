using Debug = System.Diagnostics.Debug;

namespace DotNext.Runtime.Caching;

public partial class ConcurrentCache<TKey, TValue>
{
    private abstract class EvictionDeque
    {
        private protected readonly int index;
        private readonly Table table;
        private CommandQueueReader commandReader;
        internal EvictionDeque Next;
        private protected KeyValuePair? first, last;
        private int count;

        private protected EvictionDeque(int index, CommandQueueReader reader, Table table)
        {
            Next = this;
            commandReader = reader;
            this.index = index;
            this.table = table;
        }

        internal void Clear() => first = last = null;

        internal void DrainCommandQueue(Command last, ref Command commandQueueWritePosition, Action<TKey, TValue>? evictionHandler)
        {
            for (var lastReached = false; !lastReached;)
            {
                var command = commandReader.Read(last, out lastReached);
                if (command.Pair.IsAlive.Value)
                {
                    switch (command.Type)
                    {
                        case CommandType.Read:
                            Read(command.Pair);
                            break;
                        case CommandType.Add:
                            Add(command.Pair, ref commandQueueWritePosition, evictionHandler);
                            break;
                        case CommandType.Remove:
                            Remove(command.Pair);
                            break;
                    }
                }
                else
                {
                    Remove(command.Pair);
                }
            }
        }

        private protected abstract void Read(KeyValuePair pair);

        private void Add(KeyValuePair pair, ref Command commandQueueWritePosition, Action<TKey, TValue>? evictionHandler)
        {
            if (first is null || last is null)
            {
                first = last = pair;
            }
            else
            {
                first.GetPrevious(index) = pair;
                first = pair;
            }

            count += 1;
            Evict(ref commandQueueWritePosition, evictionHandler);
        }

        private void Evict(ref Command commandQueueWritePosition, Action<TKey, TValue>? evictionHandler)
        {
            for (KeyValuePair? last; count > table.Capacity; count--)
            {
                last = this.last;
                if (last is not null && last.IsAlive.TrueToFalse())
                {
                    table.Remove(last);
                    Enqueue(ref commandQueueWritePosition, CommandType.Remove, last);
                    evictionHandler?.Invoke(last.Key, last.Value);
                }
            }
        }

        private void Remove(KeyValuePair pair)
        {
            ref var previous = ref pair.GetPrevious(index);
            ref var next = ref pair.GetNext(index);

            if (ReferenceEquals(first, pair))
                first = next;

            if (ReferenceEquals(last, pair))
                last = previous;

            Link(previous, next);
            previous = next = null;

            count -= 1;
        }

        private protected void Link(KeyValuePair? previous, KeyValuePair? next)
        {
            if (previous is not null)
                previous.GetNext(index) = next;

            if (next is not null)
                next.GetPrevious(index) = previous;
        }
    }

    private sealed class LRUEvictionStrategy : EvictionDeque
    {
        internal LRUEvictionStrategy(int index, CommandQueueReader reader, Table table)
            : base(index, reader, table)
        {
        }

        private protected override void Read(KeyValuePair pair)
        {
            if (first is null || last is null)
            {
                first = last = pair;
            }
            else if (!ReferenceEquals(first, pair))
            {
                ref var previous = ref pair.GetPrevious(index);
                ref var next = ref pair.GetNext(index);

                Link(previous, next);
                previous = null;

                first.GetPrevious(index) = pair;
                first = pair;
            }
        }
    }

    private sealed class LFUEvictionStrategy : EvictionDeque
    {
        internal LFUEvictionStrategy(int index, CommandQueueReader reader, Table table)
            : base(index, reader, table)
        {
        }

        private protected override void Read(KeyValuePair pair)
        {
            if (first is null || last is null)
            {
                first = last = pair;
            }
            else if (!ReferenceEquals(first, pair))
            {
                ref var previous = ref pair.GetPrevious(index);
                ref var next = ref pair.GetNext(index);

                Debug.Assert(previous is not null);

                // replace previous with the current pair
                previous.GetNext(index) = next;
                (next = previous).GetPrevious(index) = pair;
                previous = next.GetPrevious(index);
            }
        }
    }

    private readonly int concurrencyLevel;
    private volatile EvictionDeque currentDeque;
}