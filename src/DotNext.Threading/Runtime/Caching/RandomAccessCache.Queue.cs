using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Runtime.Caching;

public partial class RandomAccessCache<TKey, TValue>
{
    private volatile KeyValuePair promotionHead, promotionTail;

    private void Promote(KeyValuePair newPair)
    {
        KeyValuePair? currentTail, tmp = promotionTail;
        do
        {
            currentTail = tmp;
            tmp = Interlocked.CompareExchange(ref currentTail.NextInQueue, newPair, null);
        } while (tmp is not null);

        // attempt to install a new tail. Do not retry if failed, competing thread installed more recent version of it
        Interlocked.CompareExchange(ref promotionTail, newPair, currentTail);

        currentTail.TrySetResult();
    }

    partial class KeyValuePair
    {
        internal KeyValuePair? NextInQueue;
        private volatile uint promoted;

        internal bool TryConsumePromotion() => Interlocked.Exchange(ref promoted, 1U) is 0U;
        
        [ExcludeFromCodeCoverage]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal int QueueLength
        {
            get
            {
                var count = 0;
                for (var current = this; current is not null; current = current.NextInQueue)
                {
                    count++;
                }

                return count;
            }
        }
    }

    // Never call GetValue on this class, it has no storage for TValue.
    // It is used as a stub for the first element in the notification queue to keep task completion source
    private sealed class FakeKeyValuePair() : KeyValuePair(default!, 0)
    {
        public override string ToString() => "Fake KV Pair";
    }
}