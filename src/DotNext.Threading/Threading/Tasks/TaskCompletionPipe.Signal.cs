using System.Diagnostics;

namespace DotNext.Threading.Tasks;

using Pooling;

public partial class TaskCompletionPipe<T>
{
    private sealed class Signal : LinkedValueTaskCompletionSource<bool>, IPooledManualResetCompletionSource<Action<Signal>>
    {
        private Action<Signal>? completionCallback;

        protected override void AfterConsumed() => completionCallback?.Invoke(this);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal bool NeedsRemoval => CompletionData is null;

        Action<Signal>? IPooledManualResetCompletionSource<Action<Signal>>.OnConsumed
        {
            get => completionCallback;
            set => completionCallback = value;
        }
    }

    private ValueTaskPool<bool, Signal, Action<Signal>> pool;
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

        LinkedValueTaskCompletionSource<bool> result = pool.Get();
        waitQueue.Add(result);
        return result;
    }
}