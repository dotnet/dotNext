using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading;

using Collections.Concurrent;
using Tasks;

public static partial class AsyncBridge
{
    private sealed class CancellationTokenValueTask : ValueTaskCompletionSource
    {
        private readonly Action<CancellationTokenValueTask> backToPool;
        internal new bool CompleteAsCanceled;

        internal CancellationTokenValueTask(Action<CancellationTokenValueTask> backToPool)
            => this.backToPool = backToPool;

        protected override void AfterConsumed()
        {
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

        protected override Exception? OnCanceled(CancellationToken token)
            => CompleteAsCanceled ? new OperationCanceledException(token) : null;
    }

    private static readonly Action<CancellationTokenValueTask> CancellationTokenValueTaskCompletionCallback
        = new UnboundedObjectPool<CancellationTokenValueTask>().Return;

    private static UnboundedObjectPool<CancellationTokenValueTask> TokenPool
    {
        get
        {
            Debug.Assert(CancellationTokenValueTaskCompletionCallback.Target is UnboundedObjectPool<CancellationTokenValueTask>);

            return Unsafe.As<UnboundedObjectPool<CancellationTokenValueTask>>(CancellationTokenValueTaskCompletionCallback.Target);
        }
    }

    private sealed class TaskToCancellationTokenCallback
    {
        private volatile WeakReference<CancellationTokenSource>? sourceRef;

        internal TaskToCancellationTokenCallback(out CancellationToken token)
        {
            var source = new CancellationTokenSource();
            token = source.Token;
            sourceRef = new(source);
        }

        private bool TryStealSource([NotNullWhen(true)] out CancellationTokenSource? source)
        {
            source = null;
            return Interlocked.Exchange(ref sourceRef, null) is { } weakRef
                   && weakRef.TryGetTarget(out source);
        }

        internal bool TryDispose()
        {
            if (TryStealSource(out var source))
            {
                source.Dispose();
            }

            return source is not null;
        }

        internal void CancelAndDispose()
        {
            if (TryStealSource(out var source))
            {
                try
                {
                    source.Cancel();
                }
                finally
                {
                    source.Dispose();
                }
            }
        }
    }
}