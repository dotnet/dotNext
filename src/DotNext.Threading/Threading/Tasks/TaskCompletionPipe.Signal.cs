using System.Diagnostics;

namespace DotNext.Threading.Tasks;

using Pooling;

public partial class TaskCompletionPipe<T>
{
    private sealed class Signal : LinkedValueTaskCompletionSource<bool>
    {
        private TaskCompletionPipe<T>? owner;

        internal void Initialize(TaskCompletionPipe<T> owner)
        {
            this.owner = owner;
        }

        protected override void CleanUp()
            => owner = null;

        protected override void AfterConsumed()
        {
            if (owner is { } ownerCopy && TryReset(out _))
                ownerCopy.OnCompleted(this);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal bool NeedsRemoval => CompletionData is null;
    }

    private ValueTaskPool<bool> pool;
    private LinkedValueTaskCompletionSource<bool>.LinkedList waitQueue;

    // detach all suspended callers to process out of the monitor lock
    private LinkedValueTaskCompletionSource<bool>? DetachWaitQueue()
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        var result = waitQueue.First;
        waitQueue = default;
        return result;
    }

    private LinkedValueTaskCompletionSource<bool> EnqueueNode()
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        var result = pool.Rent<Signal>();
        result.Initialize(this);
        waitQueue.Add(result);
        return result;
    }
}