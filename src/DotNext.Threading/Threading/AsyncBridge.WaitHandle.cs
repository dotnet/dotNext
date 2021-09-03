using System;
using System.Collections.Concurrent;
using System.Threading;

namespace DotNext.Threading
{
    using DotNext;
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

            internal RegisteredWaitHandle Handle
            {
                set => handle = value;
            }

            protected override void BeforeCompleted(Result<bool> result)
                => Interlocked.Exchange(ref handle, null)?.Unregister(null);

            protected override void AfterConsumed()
            {
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
        }

        private static readonly WaitHandleValueTaskPool handlePool = new();
    }
}