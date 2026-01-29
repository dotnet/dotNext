namespace DotNext.Threading;

using Collections.Concurrent;
using Tasks;

public static partial class AsyncBridge
{
    private static readonly IObjectPool<WaitHandleValueTask> HandlePool = new UnboundedObjectPool<WaitHandleValueTask>();
    
    private sealed class WaitHandleValueTask : ValueTaskCompletionSource<bool>
    {
        private volatile RegisteredWaitHandle? handle;

        internal RegisteredWaitHandle Registration
        {
            set => handle = value;
        }

        protected override void AfterConsumed()
        {
            Interlocked.Exchange(ref handle, null)?.Unregister(null);
            Reset();

            if (Interlocked.Increment(ref poolSize) > maxPoolSize)
            {
                Interlocked.Decrement(ref poolSize);
            }
            else
            {
                HandlePool.Return(this);
            }
        }
    }
}