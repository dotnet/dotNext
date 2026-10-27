using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Collections.Concurrent;

using Patterns;

partial struct MpscQueue
{
    public abstract class Item : ISupplier<IThreadPoolWorkItem, bool>
    {
        // null, or Command, or Sentinel.Instance
        internal object? NextInQueue;
        private volatile IThreadPoolWorkItem? notification;

        public void NotifyItemAttached()
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
        bool ISupplier<IThreadPoolWorkItem, bool>.Invoke(IThreadPoolWorkItem continuation)
            => Interlocked.CompareExchange(ref notification, continuation, null) is null;
        
        [ExcludeFromCodeCoverage]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public int QueueLength
        {
            get
            {
                var count = 0;
                for (var current = this; current is not null; current = current.NextInQueue as Item)
                {
                    if (current.HasNext)
                        count++;
                }

                return count;
            }
        }
    }
    
    private sealed class QueueHead : Item
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