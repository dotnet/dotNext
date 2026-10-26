using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Collections.Concurrent;

using Threading.Tasks;

internal partial struct MpscQueue
{
    // Queue has multiple producers and a single consumer. Consumer doesn't require special lock-free approach to dequeue.
    private Item queueTail, queueHead;

    public MpscQueue() => queueHead = queueTail = new QueueHead();
    
    public void Enqueue(Item newPair)
    {
        Item currentTail;
        do
        {
            currentTail = queueTail;
        } while (Interlocked.CompareExchange(ref currentTail.NextInQueue, newPair, null) is not null);

        // attempt to install a new tail. Do not retry if failed, competing thread installed more recent version of it
        Interlocked.CompareExchange(ref queueTail, newPair, currentTail);

        currentTail.NotifyDequeued();
    }

    public ValueTask<bool> WaitAsync(CancelableValueTaskCompletionSource source)
        => source.WaitAsync(queueHead);
    
    public Item? TryDequeue()
    {
        if (queueHead.NextInQueue is Item newHead)
        {
            queueHead.NextInQueue = Sentinel.Instance;
            queueHead = newHead;
            return newHead;
        }

        return null;
    }
    
    [ExcludeFromCodeCoverage]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public readonly int Size => queueHead?.QueueLength ?? 0;
}