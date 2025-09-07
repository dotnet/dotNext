using System.Diagnostics;

namespace DotNext.Threading;

using Tasks;

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

    private protected class WaitNode :
        LinkedValueTaskCompletionSource<bool>,
        IValueTaskFactory<ValueTask>,
        IValueTaskFactory<ValueTask<bool>>
    {
        private WaitNodeFlags flags;
        private QueuedSynchronizer? owner;

        // stores information about suspended caller for debugging purposes
        internal object? CallerInfo { get; private set; }

        protected override void CleanUp()
        {
            CallerInfo = null;
            owner = null;
            base.CleanUp();
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal bool NeedsRemoval => CompletionData is null;

        internal void Initialize(QueuedSynchronizer owner, object? callerInfo, bool throwOnTimeout)
        {
            flags |= throwOnTimeout ? WaitNodeFlags.ThrowOnTimeout : WaitNodeFlags.None;
            this.owner = owner;
            CallerInfo = callerInfo;
        }

        internal bool DrainOnReturn
        {
            get => (flags & WaitNodeFlags.DrainOnReturn) is not 0;
            set => flags |= value ? WaitNodeFlags.DrainOnReturn : WaitNodeFlags.None;
        }

        protected sealed override void AfterConsumed()
        {
            if (owner is { } ownerCopy && TryReset(out _))
                ownerCopy.ReturnNode(this);
        }

        protected sealed override Result<bool> OnTimeout()
            => (flags & WaitNodeFlags.ThrowOnTimeout) is not 0 ? base.OnTimeout() : false;

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
    }
    
    [Flags]
    private protected enum WaitNodeFlags
    {
        None = 0,
        ThrowOnTimeout = 1,
        DrainOnReturn = ThrowOnTimeout << 1,
    }
    
    private protected interface INodeMapper<in TNode, out TValue>
        where TNode : WaitNode
    {
        public static abstract TValue GetValue(TNode node);
    }
}