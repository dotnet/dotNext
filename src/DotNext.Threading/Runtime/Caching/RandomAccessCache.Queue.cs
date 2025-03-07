using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Runtime.Caching;

using Patterns;

public partial class RandomAccessCache<TKey, TValue>
{
    // Queue has multiple producers and a single consumer. Consumer doesn't require special lock-free approach to dequeue.
    private KeyValuePair queueTail, queueHead;

    private void Promote(KeyValuePair newPair)
    {
        KeyValuePair currentTail;
        do
        {
            currentTail = queueTail;
        } while (Interlocked.CompareExchange(ref currentTail.NextInQueue, newPair, null) is not null);

        // attempt to install a new tail. Do not retry if failed, competing thread installed more recent version of it
        Interlocked.CompareExchange(ref queueTail, newPair, currentTail);

        currentTail.Notify();
    }

    internal partial class KeyValuePair
    {
        // null, or KeyValuePair, or Sentinel.Instance
        internal object? NextInQueue;
        private volatile IThreadPoolWorkItem? notification;

        internal void Notify()
        {
            if (Interlocked.Exchange(ref notification, SentinelNotification.Instance) is { } callback
                && !ReferenceEquals(callback, SentinelNotification.Instance))
            {
                ThreadPool.UnsafeQueueUserWorkItem(callback, preferLocal: false);
            }
        }

        [ExcludeFromCodeCoverage]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool IsNotified => ReferenceEquals(notification, SentinelNotification.Instance);

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
                for (var current = this; current is not null; current = current.NextInQueue as KeyValuePair)
                {
                    if (current.IsNotified)
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

file sealed class SentinelNotification : IThreadPoolWorkItem, ISingleton<SentinelNotification>
{
    public static SentinelNotification Instance { get; } = new();
    
    private SentinelNotification()
    {
    }

    void IThreadPoolWorkItem.Execute()
    {
    }
}