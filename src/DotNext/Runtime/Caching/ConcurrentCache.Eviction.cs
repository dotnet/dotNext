using System.Diagnostics;

namespace DotNext.Runtime.Caching;

public partial class ConcurrentCache<TKey, TValue>
{
    private readonly CacheEvictionPolicy evictionPolicy;
    private int evictionListSize;
    private KeyValuePair? firstPair, lastPair;
    private Action<TKey, TValue>? evictionHandler;

    private KeyValuePair? Execute(CommandType command, KeyValuePair target)
    {
        KeyValuePair? evictedPair = null;

        switch (command)
        {
            case CommandType.Read:
                Read();
                break;
            case CommandType.Add:
                Add();
                evictedPair = evictionListSize > buckets.Length ? Evict() : null;
                break;
            case CommandType.Remove:
                Remove();
                break;
        }

        return evictedPair;

        void Add()
        {
            AddFirst(target);
            evictionListSize += 1;
        }

        void Remove()
        {
            Detach(target);
            evictionListSize--;
        }

        void Read()
        {
            if (!ReferenceEquals(lastPair, target))
            {
                switch (evictionPolicy)
                {
                    case CacheEvictionPolicy.LFU:
                        ReadLFU();
                        break;
                    default:
                        ReadLRU();
                        break;
                }
            }
        }

        void ReadLFU()
        {
            var parent = target.Links.Previous?.Links.Previous;
            Detach(target);

            if (parent is null)
                AddFirst(target);
            else
                Append(parent, target);
        }

        void ReadLRU()
        {
            Detach(target);
            AddFirst(target);
        }
    }

    private static void Append(KeyValuePair parent, KeyValuePair child)
    {
        child.Links.Previous = parent;

        if ((child.Links.Next = parent.Links.Next) is not null)
            child.Links.Next.Links.Previous = child;
    }

    private void AddFirst(KeyValuePair pair)
    {
        if (firstPair is null || lastPair is null)
        {
            firstPair = lastPair = pair;
        }
        else
        {
            lastPair.Links.Previous = pair;
            pair.Links.Next = firstPair;
            firstPair = pair;
        }
    }

    private KeyValuePair Evict()
    {
        var last = lastPair;
        Debug.Assert(last is not null);

        Detach(last);
        Remove(last);
        last.Next = null;
        evictionListSize--;

        return last;
    }

    private void Detach(KeyValuePair pair)
    {
        if (ReferenceEquals(firstPair, pair))
            firstPair = pair.Links.Next;

        if (ReferenceEquals(lastPair, pair))
            lastPair = pair.Links.Previous;

        MakeLink(pair.Links.Previous, pair.Links.Next);
    }

    private static void MakeLink(KeyValuePair? previous, KeyValuePair? next)
    {
        if (previous is not null)
            previous.Links.Next = next;

        if (next is not null)
            next.Links.Previous = previous;
    }

    private void OnEviction(KeyValuePair? pair)
    {
        var evictionHandler = this.evictionHandler;

        if (evictionHandler is not null)
        {
            for (KeyValuePair? next; pair is not null; pair = next)
            {
                next = pair.Next;
                pair.Clear();
                evictionHandler.Invoke(pair.Key, pair.Value);
            }
        }
    }

    /// <summary>
    /// Gets or sets a handler that can be used to capture evicted cache items.
    /// </summary>
    public event Action<TKey, TValue> Eviction
    {
        add => evictionHandler += value;
        remove => evictionHandler -= value;
    }
}