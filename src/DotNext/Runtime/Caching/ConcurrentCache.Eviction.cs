using System.Diagnostics;

namespace DotNext.Runtime.Caching;

public partial class ConcurrentCache<TKey, TValue>
{
    private readonly Action<TKey, TValue>? evictionHandler;
    private readonly CacheEvictionPolicy evictionPolicy;
    private int evictionListSize;
    private KeyValuePair? firstPair, lastPair;

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

    private static void OnEviction(KeyValuePair? pair, Action<TKey, TValue> evictionHandler)
    {
        for (KeyValuePair? next; pair is not null; pair = next)
        {
            next = pair.Next;
            pair.Clear();
            evictionHandler.Invoke(pair.Key, GetValue(pair));
        }
    }

    private void OnEviction(KeyValuePair? pair)
    {
        if (pair is not null && evictionHandler is not null)
        {
            if (ExecuteEvictionAsynchronously)
                ThreadPool.QueueUserWorkItem(static args => OnEviction(args.Item1, args.Item2), (pair, evictionHandler), preferLocal: true);
            else
                OnEviction(pair, evictionHandler);
        }
    }

    /// <summary>
    /// Gets or sets a handler that can be used to capture evicted cache items.
    /// </summary>
    public Action<TKey, TValue>? Eviction
    {
        get => evictionHandler;
        init => evictionHandler = value;
    }

    /// <summary>
    /// Gets or sets a value indicating that <see cref="Eviction"/> callback must be executed asynchronously.
    /// </summary>
    public bool ExecuteEvictionAsynchronously
    {
        get;
        init;
    }
}