using System.Collections.Concurrent;
using Debug = System.Diagnostics.Debug;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

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

        internal void Complete(object? token, bool timedOut)
        {
            Debug.Assert(token is short);

            TrySetResult(Unsafe.Unbox<short>(token), !timedOut);
        }
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