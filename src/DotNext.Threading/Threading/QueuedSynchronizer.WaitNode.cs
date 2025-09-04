using System.Diagnostics;

namespace DotNext.Threading;

using Tasks;
using Tasks.Pooling;

partial class QueuedSynchronizer
{
    private interface IValueTaskFactory<out T> : ISupplier<TimeSpan, CancellationToken, T>
        where T : struct, IEquatable<T>
    {
        static abstract T SuccessfulTask { get; }
        
        static abstract T TimedOutTask { get; }

        static abstract T FromException(Exception e);

        static abstract T FromCanceled(CancellationToken token);
        
        static abstract bool ThrowOnTimeout { get; }
    }

    private protected abstract class WaitNode : LinkedValueTaskCompletionSource<bool>, IValueTaskFactory<ValueTask>,
        IValueTaskFactory<ValueTask<bool>>
    {
        private bool throwOnTimeout;

        // stores information about suspended caller for debugging purposes
        internal object? CallerInfo { get; private set; }

        protected override void CleanUp()
        {
            CallerInfo = null;
            base.CleanUp();
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal bool NeedsRemoval => CompletionData is null;

        internal void Initialize(object? callerInfo, bool throwOnTimeout)
        {
            this.throwOnTimeout = throwOnTimeout;
            CallerInfo = callerInfo;
        }

        protected sealed override Result<bool> OnTimeout()
            => throwOnTimeout ? base.OnTimeout() : false;

        private protected static void AfterConsumed<T>(T node)
            where T : WaitNode, IPooledManualResetCompletionSource<Action<T>>
            => node.OnConsumed?.Invoke(node);

        static ValueTask IValueTaskFactory<ValueTask>.SuccessfulTask => ValueTask.CompletedTask;

        static ValueTask<bool> IValueTaskFactory<ValueTask<bool>>.SuccessfulTask => ValueTask.FromResult(true);

        static ValueTask IValueTaskFactory<ValueTask>.TimedOutTask => ValueTask.FromException(new TimeoutException());

        static ValueTask<bool> IValueTaskFactory<ValueTask<bool>>.TimedOutTask => ValueTask.FromResult(false);

        static ValueTask<bool> IValueTaskFactory<ValueTask<bool>>.FromException(Exception e)
            => ValueTask.FromException<bool>(e);

        static ValueTask IValueTaskFactory<ValueTask>.FromException(Exception e)
            => ValueTask.FromException(e);

        static ValueTask<bool> IValueTaskFactory<ValueTask<bool>>.FromCanceled(CancellationToken token)
            => ValueTask.FromCanceled<bool>(token);

        static ValueTask IValueTaskFactory<ValueTask>.FromCanceled(CancellationToken token)
            => ValueTask.FromCanceled(token);

        static bool IValueTaskFactory<ValueTask<bool>>.ThrowOnTimeout => false;

        static bool IValueTaskFactory<ValueTask>.ThrowOnTimeout => true;

        private bool TrySetResult(in Result<bool> result, out bool resumable)
            => TrySetResult(Sentinel.Instance, completionToken: null, result, out resumable);

        internal bool TrySetResult(out bool resumable)
            => TrySetResult(result: true, out resumable);

        internal bool TrySetException(Exception e, out bool resumable)
            => TrySetResult(new Result<bool>(e), out resumable);

        internal bool TrySetResult<TLockManager>(ref TLockManager manager, out bool resumable)
            where TLockManager : ILockManager
        {
            if (!manager.IsLockAllowed)
                return resumable = false;

            if (TrySetResult(out resumable))
                manager.AcquireLock();

            return true;
        }
    }

    private protected sealed class DefaultWaitNode : WaitNode, IPooledManualResetCompletionSource<Action<DefaultWaitNode>>
    {
        protected override void AfterConsumed() => AfterConsumed(this);

        Action<DefaultWaitNode>? IPooledManualResetCompletionSource<Action<DefaultWaitNode>>.OnConsumed { get; set; }
    }
}