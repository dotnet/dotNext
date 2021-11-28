using System.Collections.Concurrent;

namespace DotNext.Threading;

using Tasks;

public static partial class AsyncBridge
{
    private sealed class WaitHandleValueTask : ValueTaskCompletionSource<bool>
    {
        private readonly Action<WaitHandleValueTask> backToPool;
        private volatile RegisteredWaitHandle? handle;

        internal WaitHandleValueTask(Action<WaitHandleValueTask> backToPool)
        {
            this.backToPool = backToPool;
            Interlocked.Increment(ref instantiatedTasks);
        }

        internal RegisteredWaitHandle Registration
        {
            set => handle = value;
        }

        protected override void AfterConsumed()
        {
            Interlocked.Exchange(ref handle, null)?.Unregister(null);
            Interlocked.Decrement(ref instantiatedTasks);
            backToPool(this);
        }

        private void Complete(short token, bool timedOut)
            => TrySetResult(token, !timedOut);

        internal void Complete(object? token, bool timedOut)
            => Complete((short)token!, timedOut);
    }

    private sealed class WaitHandleValueTaskPool : ConcurrentBag<WaitHandleValueTask>
    {
        internal void Return(WaitHandleValueTask vt)
        {
            if (vt.TryReset(out _))
                Add(vt);
        }
    }

    private static readonly WaitHandleValueTaskPool HandlePool = new();
    private static readonly Action<WaitHandleValueTask> WaitHandleTaskCompletionCallback = HandlePool.Return;
}