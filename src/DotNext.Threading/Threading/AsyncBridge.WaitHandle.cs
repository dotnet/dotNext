using Debug = System.Diagnostics.Debug;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading;

using Collections.Concurrent;
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

            if (!TryReset(out _))
            {
                // cannot be returned to the pool
            }
            else if (Interlocked.Increment(ref instantiatedTasks) > maxPoolSize)
            {
                Interlocked.Decrement(ref instantiatedTasks);
            }
            else
            {
                backToPool(this);
            }
        }
    }

    private static readonly Action<WaitHandleValueTask> WaitHandleTaskCompletionCallback
        = new UnboundedObjectPool<WaitHandleValueTask>().Return;

    private static UnboundedObjectPool<WaitHandleValueTask> HandlePool
    {
        get
        {
            Debug.Assert(WaitHandleTaskCompletionCallback.Target is UnboundedObjectPool<WaitHandleValueTask>);

            return Unsafe.As<UnboundedObjectPool<WaitHandleValueTask>>(WaitHandleTaskCompletionCallback.Target);
        }
    }
}