using System.Runtime.InteropServices;

namespace DotNext.Threading;

partial class QueuedSynchronizer
{
    /// <summary>
    /// The default lock manager with no state.
    /// </summary>
    private protected static DefaultLockManager<WaitNode> DefaultManager;
    
    private protected interface ILockManager
    {
        bool IsLockAllowed { get; }

        void AcquireLock();

        static virtual bool RequiresEmptyQueue => true;
    }
    
    [StructLayout(LayoutKind.Auto)]
    private protected readonly struct DefaultLockManager<TNode> : ILockManager, IConsumer<TNode>
        where TNode : WaitNode
    {
        void IConsumer<TNode>.Invoke(TNode node)
        {
        }

        bool ILockManager.IsLockAllowed => false;
        
        void ILockManager.AcquireLock()
        {
        }
    }
}