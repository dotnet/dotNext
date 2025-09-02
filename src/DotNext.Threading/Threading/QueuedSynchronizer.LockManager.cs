using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

partial class QueuedSynchronizer
{
    private interface INodeInitializer<in TNode> : IConsumer<TNode>
        where TNode : WaitNode
    {
        WaitNodeFlags Flags { get; }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct StaticInitializer<TNode, TLockManager>(WaitNodeFlags flags) : INodeInitializer<TNode>
        where TNode : WaitNode
        where TLockManager : struct, ILockManager<TNode>
    {
        WaitNodeFlags INodeInitializer<TNode>.Flags => flags;

        void IConsumer<TNode>.Invoke(TNode node) => TLockManager.InitializeNode(node);
    }

    // TODO: Replace with allows ref anti-constraint and ref struct
    [StructLayout(LayoutKind.Auto)]
    private readonly struct NodeInitializer<TNode, TLockManager>([ConstantExpected] WaitNodeFlags flags, ref TLockManager manager)
        : INodeInitializer<TNode>
        where TNode : WaitNode
        where TLockManager : struct, ILockManager, IConsumer<TNode>
    {
        private readonly unsafe void* managerOnStack = Unsafe.AsPointer(ref manager);

        WaitNodeFlags INodeInitializer<TNode>.Flags => flags;

        unsafe void IConsumer<TNode>.Invoke(TNode node)
            => Unsafe.AsRef<TLockManager>(managerOnStack).Invoke(node);
    }

    private protected interface ILockManager
    {
        bool IsLockAllowed { get; }

        void AcquireLock();

        static virtual bool RequiresEmptyQueue => true;
    }

    private protected interface ILockManager<in TNode> : ILockManager
        where TNode : WaitNode
    {
        static virtual void InitializeNode(TNode node)
        {
        }
    }
}