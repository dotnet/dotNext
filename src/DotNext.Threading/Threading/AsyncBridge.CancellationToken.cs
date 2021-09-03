using System;
using System.Collections.Concurrent;
using System.Threading;

namespace DotNext.Threading
{
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
        }

        private static readonly CancellationTokenValueTaskPool tokenPool = new();
    }
}