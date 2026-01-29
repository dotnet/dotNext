using System.Diagnostics;

namespace DotNext.Threading;

using Tasks;

partial class QueuedSynchronizer
{
    private protected class WaitNode : LinkedValueTaskCompletionSource<bool>, IValueTaskFactory
    {
        private WaitNodeFlags flags;
        private QueuedSynchronizer? owner;

        // stores information about suspended caller for debugging purposes
        internal object? CallerInfo { get; private set; }

        protected override void CleanUp()
        {
            CallerInfo = null;
            owner = null;
            flags = WaitNodeFlags.None;
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

        protected sealed override void AfterConsumed()
            => owner?.ReturnNode(this);

        protected sealed override Result<bool> GetTimeoutResult()
            => (flags & WaitNodeFlags.ThrowOnTimeout) is not 0 ? base.GetTimeoutResult() : false;
    }
    
    [Flags]
    private protected enum WaitNodeFlags
    {
        None = 0,
        ThrowOnTimeout = 1
    }
    
    private protected interface INodeMapper<in TNode, out TValue>
        where TNode : WaitNode
    {
        public static abstract TValue GetValue(TNode node);
    }
}