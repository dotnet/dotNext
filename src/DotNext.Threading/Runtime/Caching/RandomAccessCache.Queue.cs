namespace DotNext.Runtime.Caching;

using Collections.Concurrent;

public partial class RandomAccessCache<TKey, TValue>
{
    private MpscQueue queue;

    // Notifies the eviction thread that the pair is removed from the cache, or all the pairs if Target is null.
    // The signal passes through the promotion queue, so it is processed after the promotion of the removed pair.   
    private sealed class InvalidationSignal : MpscQueue.Item
    {
        internal required KeyValuePair? Target;

        public override string ToString() => Target is null ? "Invalidate all" : $"Invalidate {Target.Key}";
    }

    private void NotifyRemoved(KeyValuePair removed) => queue.Enqueue(new InvalidationSignal { Target = removed });
}