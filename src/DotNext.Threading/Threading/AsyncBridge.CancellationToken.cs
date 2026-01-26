using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading;

using Collections.Concurrent;
using Tasks;

public static partial class AsyncBridge
{
    private static readonly IObjectPool<CancellationTokenValueTask> TokenPool = new UnboundedObjectPool<CancellationTokenValueTask>();
    
    private sealed class CancellationTokenValueTask : ValueTaskCompletionSource
    {
        internal new bool CompleteAsCanceled;

        protected override void AfterConsumed()
        {
            if (!TryReset(out _))
            {
                // cannot be returned to the pool
            }
            else if (Interlocked.Increment(ref poolSize) > maxPoolSize)
            {
                Interlocked.Decrement(ref poolSize);
            }
            else
            {
                TokenPool.Return(this);
            }
        }

        protected override Exception? OnCanceled(CancellationToken token)
            => CompleteAsCanceled ? new OperationCanceledException(token) : null;
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