namespace DotNext.Threading;

partial class QueuedSynchronizer
{
    private protected interface ILockManager
    {
        bool IsLockAllowed { get; }

        void AcquireLock();

        static virtual bool RequiresEmptyQueue => true;
    }

    private protected interface ILockManager<in TNode> : ILockManager
        where TNode : WaitNode
    {
        static virtual void Initialize(TNode node)
        {
        }
    }

    private bool TryAcquire<TLockManager>(TLockManager manager)
        where TLockManager : struct, ILockManager, allows ref struct
    {
        AssertInternalLockState();

        if ((!TLockManager.RequiresEmptyQueue || IsEmptyQueue) && manager.IsLockAllowed)
        {
            manager.AcquireLock();
            return true;
        }

        return false;
    }

    private protected System.Threading.Lock.Scope TryAcquire<TLockManager>(TLockManager manager, out bool acquired)
        where TLockManager : struct, ILockManager, allows ref struct
    {
        var scope = waitQueue.SyncRoot.EnterScope();
        acquired = TryAcquire(manager);
        return scope;
    }

    private protected T EndAcquisition<T, TBuilder, TNode, TLockManager>(ref TBuilder builder, scoped TLockManager manager)
        where T : struct, IEquatable<T>
        where TNode : WaitNode, new()
        where TBuilder : struct, ITaskBuilder<T>, allows ref struct
        where TLockManager : struct, ILockManager<TNode>, allows ref struct
    {
        switch (builder.IsCompleted)
        {
            case true:
                goto default;
            case false when Acquire<T, TBuilder, TNode>(ref builder, TryAcquire(manager)) is { } node:
                TLockManager.Initialize(node);
                goto default;
            default:
                return builder.Build();
        }
    }

    private protected T EndAcquisition<T, TBuilder, TNode, TLockManager>(
        PendingTaskInterruptedException e,
        ref TBuilder builder,
        TLockManager manager)
        where T : struct, IEquatable<T>
        where TNode : WaitNode, new()
        where TBuilder : struct, ITaskBuilder<T>, IWaitQueueProvider, allows ref struct
        where TLockManager : struct, ILockManager<TNode>, allows ref struct
    {
        WaitQueueScope scope;
        if (builder.IsCompleted)
        {
            scope = default;
        }
        else
        {
            scope = builder.CaptureWaitQueue();
            scope.SignalAll(e);

            if (Acquire<T, TBuilder, TNode>(ref builder, TryAcquire(manager)) is { } node)
                TLockManager.Initialize(node);
        }

        var task = builder.Build();
        scope.ResumeSuspendedCallers();
        return task;
    }
}