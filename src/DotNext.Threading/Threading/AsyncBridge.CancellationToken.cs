using System.Collections.Concurrent;
using Debug = System.Diagnostics.Debug;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

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

    private static readonly Action<CancellationTokenValueTask> CancellationTokenValueTaskCompletionCallback = new CancellationTokenValueTaskPool().Return;

    private static CancellationTokenValueTaskPool TokenPool
    {
        get
        {
            Debug.Assert(CancellationTokenValueTaskCompletionCallback.Target is CancellationTokenValueTaskPool);

            return Unsafe.As<CancellationTokenValueTaskPool>(CancellationTokenValueTaskCompletionCallback.Target);
        }
    }
}