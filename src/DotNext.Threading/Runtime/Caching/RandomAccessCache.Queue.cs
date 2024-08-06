namespace DotNext.Runtime.Caching;

public partial class RandomAccessCache<TKey, TValue>
{
    private KeyValuePair promotionHead, promotionTail;

    private void Promote(KeyValuePair newPair)
    {
        KeyValuePair? currentTail, tmp = Volatile.Read(in promotionTail);
        do
        {
            currentTail = tmp;
            tmp = Interlocked.CompareExchange(ref currentTail.NextPromotion, newPair, null);
        } while (tmp is not null);

        // attempt to install a new tail. Do not retry if failed, competing thread installed more recent version of it
        Interlocked.CompareExchange(ref promotionTail, newPair, currentTail);

        currentTail.TrySetResult();
    }

    partial class KeyValuePair
    {
        internal KeyValuePair? NextPromotion;
        private volatile uint promoted;

        internal bool TryConsumePromotion() => Interlocked.Exchange(ref promoted, 1U) is 0U;
    }

    // Never call GetValue on this class, it has no storage for TValue.
    // It is used as a stub for the first element in the notification queue to keep task completion source
    private sealed class FakeKeyValuePair() : KeyValuePair(default!, 0)
    {
    }
}