using System.Diagnostics;

namespace DotNext.Runtime.Caching;

public partial class ConcurrentCache<TKey, TValue>
{
    private readonly object evictionLock = new();
    private readonly Action<TKey, TValue>? evictionHandler;
    private int evictionListSize;
    private KeyValuePair? firstPair, lastPair;

    private KeyValuePair? OnAdd(KeyValuePair target)
    {
        Debug.Assert(Monitor.IsEntered(evictionLock));
        Debug.Assert(target.Removed is false);

        AddFirst(target);
        evictionListSize += 1;
        return evictionListSize > buckets.Length ? Evict() : null;
    }

    private KeyValuePair? OnRemove(KeyValuePair target)
    {
        Debug.Assert(Monitor.IsEntered(evictionLock));
        Debug.Assert(target.Removed is false);

        Detach(target);
        evictionListSize--;

        return null;
    }

    private KeyValuePair? OnReadLFU(KeyValuePair target)
    {
        Debug.Assert(Monitor.IsEntered(evictionLock));
        Debug.Assert(target.Removed is false);

        var parent = target.Links.Previous?.Links.Previous;
        Debug.Assert(ReferenceEquals(parent, target) is false);
        Detach(target);

        if (parent is null)
            AddFirst(target);
        else
            Append(parent, target);

        return null;
    }

    private KeyValuePair? OnReadLRU(KeyValuePair target)
    {
        Debug.Assert(Monitor.IsEntered(evictionLock));
        Debug.Assert(target.Removed is false);

        Detach(target);
        AddFirst(target);
        return null;
    }

    private static void Append(KeyValuePair parent, KeyValuePair child)
    {
        Debug.Assert(ReferenceEquals(parent, child) is false);

        child.Links.Previous = parent;

        if ((child.Links.Next = parent.Links.Next) is { } childNext)
            childNext.Links.Previous = child;

        parent.Links.Next = child;

        Debug.Assert(ReferenceEquals(child, child.Links.Previous) is false);
        Debug.Assert(ReferenceEquals(child, child.Links.Next) is false);
        Debug.Assert(ReferenceEquals(child.Links.Next, child.Links.Previous) is false);
    }

    private void AddFirst(KeyValuePair pair)
    {
        if (firstPair is null || lastPair is null)
        {
            firstPair = lastPair = pair;
        }
        else
        {
            Debug.Assert(ReferenceEquals(pair, firstPair) is false);

            firstPair.Links.Previous = pair;
            pair.Links.Next = firstPair;
            pair.Links.Previous = null;
            firstPair = pair;

            Debug.Assert(ReferenceEquals(pair.Links.Next, pair.Links.Previous) is false);
        }

        Debug.Assert(ReferenceEquals(pair, pair.Links.Previous) is false);
        Debug.Assert(ReferenceEquals(pair, pair.Links.Next) is false);
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
        Debug.Assert(ReferenceEquals(pair, pair.Links.Previous) is false);
        Debug.Assert(ReferenceEquals(pair, pair.Links.Next) is false);

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