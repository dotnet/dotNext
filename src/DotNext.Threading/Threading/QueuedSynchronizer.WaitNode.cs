using System.Diagnostics;

namespace DotNext.Threading;

using Tasks;

partial class QueuedSynchronizer
{
    private protected class WaitNode : LinkedValueTaskCompletionSource<bool>, IValueTaskFactory
    {
        private bool throwOnTimeout;
        private QueuedSynchronizer? owner;

        // stores information about suspended caller for debugging purposes
        internal object? CallerInfo { get; private set; }

        protected override void CleanUp()
        {
            CallerInfo = null;
            owner = null;
            throwOnTimeout = false;
            base.CleanUp();
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal bool NeedsRemoval => CompletionData is null;

        internal void Initialize(QueuedSynchronizer owner, bool throwOnTimeout)
        {
            this.throwOnTimeout = throwOnTimeout;
            this.owner = owner;
            CallerInfo = owner.CaptureCallerInformation();
        }

        protected sealed override void AfterConsumed()
            => owner?.ReturnNode(this);

        protected sealed override Result<bool> GetTimeoutResult()
            => throwOnTimeout ? base.GetTimeoutResult() : false;
    }

    private protected interface IWaitNodeFeature<out TValue> : IValueTaskFactory<bool>
    {
        TValue Feature { get; }
    }
}