using System.Diagnostics;
using System.Runtime.InteropServices;

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

        internal bool DrainOnReturn
        {
            get => (flags & WaitNodeFlags.DrainOnReturn) is not 0;
            set => flags |= value ? WaitNodeFlags.DrainOnReturn : WaitNodeFlags.None;
        }

        protected sealed override void AfterConsumed()
        {
            if (owner is null)
            {
                // nothing to do
            }
            else if (!NeedsRemoval)
            {
                owner.ReturnNode<DoNotRemoveStrategy>(new(this));
            }
            else if (DrainOnReturn)
            {
                owner.ReturnNode<RemoveAndDrainStrategy>(new(this));
            }
            else
            {
                owner.ReturnNode<RemoveStrategy>(new(this));
            }
        }

        protected sealed override Result<bool> OnTimeout()
            => (flags & WaitNodeFlags.ThrowOnTimeout) is not 0 ? base.OnTimeout() : false;

        [StructLayout(LayoutKind.Auto)]
        private readonly struct DoNotRemoveStrategy(WaitNode node) : IRemovalStrategy
        {
            bool IRemovalStrategy.Remove(ref WaitQueue queue) => false;

            LinkedValueTaskCompletionSource<bool> IRemovalStrategy.Node => node;
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly struct RemoveStrategy(WaitNode node) : IRemovalStrategy
        {
            bool IRemovalStrategy.Remove(ref WaitQueue queue)
            {
                queue.Remove(node);
                return false;
            }

            LinkedValueTaskCompletionSource<bool> IRemovalStrategy.Node => node;
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly struct RemoveAndDrainStrategy(WaitNode node) : IRemovalStrategy
        {
            bool IRemovalStrategy.Remove(ref WaitQueue queue)
                => queue.Remove(node);

            LinkedValueTaskCompletionSource<bool> IRemovalStrategy.Node => node;
        }
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