using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Runtime.Caching;

using Patterns;

public partial class RandomAccessCache<TKey, TValue>
{
    // Queue has multiple producers and a single consumer. Consumer doesn't require special lock-free approach to dequeue.
    private Command queueTail, queueHead;

    private void Enqueue(Command newPair)
    {
        Command currentTail;
        do
        {
            currentTail = queueTail;
        } while (Interlocked.CompareExchange(ref currentTail.NextInQueue, newPair, null) is not null);

        // attempt to install a new tail. Do not retry if failed, competing thread installed more recent version of it
        Interlocked.CompareExchange(ref queueTail, newPair, currentTail);

        currentTail.Dequeue();
    }
    
    internal abstract class Command
    {
        // null, or Command, or Sentinel.Instance
        internal object? NextInQueue;
        private volatile IThreadPoolWorkItem? notification;

        internal void Dequeue()
        {
            if (Interlocked.Exchange(ref notification, SentinelNotification.Instance) is { } callback
                && !ReferenceEquals(callback, SentinelNotification.Instance))
            {
                ThreadPool.UnsafeQueueUserWorkItem(callback, preferLocal: false);
            }
        }

        [ExcludeFromCodeCoverage]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected bool HasNext => ReferenceEquals(notification, SentinelNotification.Instance);

        // true - attached, false - the object is already notified
        internal bool TryAttachNotificationHandler(IThreadPoolWorkItem continuation)
            => Interlocked.CompareExchange(ref notification, continuation, null) is null;
        
        [ExcludeFromCodeCoverage]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal int QueueLength
        {
            get
            {
                var count = 0;
                for (var current = this; current is not null; current = current.NextInQueue as Command)
                {
                    if (current.HasNext)
                        count++;
                }

                return count;
            }
        }
    }

    // Notifies the eviction thread that the pair is removed from the cache, or all the pairs if Target is null.
    // The signal passes through the promotion queue, so it is processed after the promotion of the removed pair.   
    private sealed class InvalidationSignal : Command
    {
        internal required KeyValuePair? Target;

        public override string ToString() => Target is null ? "Invalidate all" : $"Invalidate {Target.Key}";
    }

    private void NotifyRemoved(KeyValuePair removed) => Enqueue(new InvalidationSignal { Target = removed });

    private sealed class QueueHead : Command
    {
        public override string ToString() => "Queue Head";
    }
}

file sealed class SentinelNotification : IThreadPoolWorkItem, ISingleton<SentinelNotification>
{
    public static SentinelNotification Instance { get; } = new();
    
    private SentinelNotification()
    {
    }

    void IThreadPoolWorkItem.Execute() => Debug.Fail("Should never be called");
}