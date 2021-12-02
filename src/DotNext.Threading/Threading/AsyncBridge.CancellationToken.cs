using System.Collections.Concurrent;

namespace DotNext.Threading;

using Tasks;

public static partial class AsyncBridge
{
    private sealed class CancellationTokenValueTask : ValueTaskCompletionSource
    {
        private readonly Action<CancellationTokenValueTask> backToPool;

        internal new bool CompleteAsCanceled;

        internal CancellationTokenValueTask(Action<CancellationTokenValueTask> backToPool)
        {
            this.backToPool = backToPool;
            Interlocked.Increment(ref instantiatedTasks);
        }

        protected override void AfterConsumed()
        {
            Interlocked.Decrement(ref instantiatedTasks);
            backToPool(this);
        }

        protected override Exception? OnCanceled(CancellationToken token)
            => CompleteAsCanceled ? new OperationCanceledException(token) : null;
    }

    private sealed class CancellationTokenValueTaskPool : ConcurrentBag<CancellationTokenValueTask>
    {
        internal void Return(CancellationTokenValueTask vt)
        {
            if (vt.TryReset(out _))
                Add(vt);
        }
    }

    private static readonly CancellationTokenValueTaskPool TokenPool = new();
    private static readonly Action<CancellationTokenValueTask> CancellationTokenValueTaskCompletionCallback = TokenPool.Return;
}