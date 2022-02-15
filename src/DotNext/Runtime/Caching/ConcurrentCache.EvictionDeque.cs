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
            AddFirst(pair);
            count += 1;
            Evict(ref commandQueueWritePosition, evictionHandler);
        }

        private protected void AddFirst(KeyValuePair pair)
        {
            if (first is null || last is null)
            {
                first = last = pair;
            }
            else
            {
                first.GetLinks(index).Previous = pair;
                pair.GetLinks(index).Next = first;
                first = pair;
            }
        }

        private protected void Append(KeyValuePair parent, KeyValuePair child)
        {
            ref var links = ref child.GetLinks(index);
            links.Previous = parent;

            if ((links.Next = parent.Next) is not null)
                links.Next.GetLinks(index).Previous = child;
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
            Detach(pair);
            count -= 1;
        }

        private protected void Detach(KeyValuePair pair)
        {
            ref var links = ref pair.GetLinks(index);

            if (ReferenceEquals(first, pair))
                first = links.Next;

            if (ReferenceEquals(last, pair))
                last = links.Previous;

            MakeLink(links.Previous, links.Next);
            links = default;
        }

        private protected void MakeLink(KeyValuePair? previous, KeyValuePair? next)
        {
            if (previous is not null)
                previous.GetLinks(index).Next = next;

            if (next is not null)
                next.GetLinks(index).Previous = previous;
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
            if (!ReferenceEquals(pair, first))
            {
                Detach(pair);
                AddFirst(pair);
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
            if (!ReferenceEquals(pair, first))
            {
                var parent = pair.GetLinks(index).Previous?.GetLinks(index).Previous;
                Detach(pair);

                if (parent is null)
                    AddFirst(pair);
                else
                    Append(parent, pair);
            }
        }
    }

    private readonly int concurrencyLevel;
    private volatile EvictionDeque currentDeque;
}