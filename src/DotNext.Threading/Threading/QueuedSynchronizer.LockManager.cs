using System.Runtime.ExceptionServices;

namespace DotNext.Threading;

partial class QueuedSynchronizer
{
    private protected interface ILockManager
    {
        bool IsLockAllowed { get; }

        void AcquireLock();

        static virtual bool RequiresEmptyQueue => true;
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
        var scope = syncRoot.EnterScope();
        acquired = TryAcquire(manager);
        return scope;
    }

    private T AcquireAsync<T, TBuilder, TNode, TLockManager>(ref TBuilder builder, TLockManager manager)
        where T : struct, IEquatable<T>
        where TNode : WaitNode, new()
        where TBuilder : struct, ITaskBuilder<T>, allows ref struct
        where TLockManager : struct, ILockManager, IConsumer<TNode>, allows ref struct
    {
        switch (builder.IsCompleted)
        {
            case true:
                goto default;
            case false when Acquire<T, TBuilder, TNode>(ref builder, TryAcquire(manager)) is { } node:
                manager.Invoke(node);
                goto default;
            default:
                return BuildTask<T, TBuilder>(ref builder);
        }
    }

    private protected ValueTask AcquireAsync<TNode, TLockManager>(TLockManager manager, TimeSpan timeout, CancellationToken token)
        where TNode : WaitNode, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>, allows ref struct
    {
        var builder = CreateTaskBuilder(timeout, token);
        return AcquireAsync<ValueTask, TimeoutAndCancellationToken, TNode, TLockManager>(ref builder, manager);
    }

    private protected ValueTask AcquireAsync<TNode, TLockManager>(TLockManager manager, CancellationToken token)
        where TNode : WaitNode, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>, allows ref struct
    {
        var builder = CreateTaskBuilder(token);
        return AcquireAsync<ValueTask, CancellationTokenOnly, TNode, TLockManager>(ref builder, manager);
    }

    private protected ValueTask AcquireAsync<TNode, TLockManager>(
        object? interruptionReason,
        TLockManager manager,
        TimeSpan timeout,
        CancellationToken token)
        where TNode : WaitNode, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>, allows ref struct
    {
        var e = new PendingTaskInterruptedException { Reason = interruptionReason };
        ExceptionDispatchInfo.SetCurrentStackTrace(e);
        
        var builder = new InterruptingTaskBuilder<ValueTask, TimeoutAndCancellationToken>
        {
            Builder = CreateTaskBuilder(timeout, token),
        };

        DrainWaitQueue(ref builder, e);
        return AcquireAsync<ValueTask, InterruptingTaskBuilder<ValueTask, TimeoutAndCancellationToken>, TNode, TLockManager>(
            ref builder,
            manager);
    }

    private protected ValueTask AcquireAsync<TNode, TLockManager>(
        object? interruptionReason,
        TLockManager manager,
        CancellationToken token)
        where TNode : WaitNode, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>, allows ref struct
    {
        var e = new PendingTaskInterruptedException { Reason = interruptionReason };
        ExceptionDispatchInfo.SetCurrentStackTrace(e);
        
        var builder = new InterruptingTaskBuilder<ValueTask, CancellationTokenOnly>
        {
            Builder = CreateTaskBuilder(token),
        };

        DrainWaitQueue(ref builder, e);
        return AcquireAsync<ValueTask, InterruptingTaskBuilder<ValueTask, CancellationTokenOnly>, TNode, TLockManager>(
            ref builder,
            manager);
    }

    private protected ValueTask<bool> TryAcquireAsync<TNode, TLockManager>(
        TLockManager manager,
        TimeSpan timeout,
        CancellationToken token)
        where TNode : WaitNode, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>, allows ref struct
    {
        var builder = CreateTaskBuilder(timeout, token);
        return AcquireAsync<ValueTask<bool>, TimeoutAndCancellationToken, TNode, TLockManager>(ref builder, manager);
    }

    private protected ValueTask<bool> TryAcquireAsync<TNode, TLockManager>(
        object? interruptionReason,
        TLockManager manager,
        TimeSpan timeout,
        CancellationToken token)
        where TNode : WaitNode, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>, allows ref struct
    {
        var e = new PendingTaskInterruptedException { Reason = interruptionReason };
        ExceptionDispatchInfo.SetCurrentStackTrace(e);
        
        var builder = new InterruptingTaskBuilder<ValueTask<bool>, TimeoutAndCancellationToken>
        {
            Builder = CreateTaskBuilder(timeout, token),
        };

        DrainWaitQueue(ref builder, e);
        return AcquireAsync<ValueTask<bool>, InterruptingTaskBuilder<ValueTask<bool>, TimeoutAndCancellationToken>, TNode, TLockManager>(
            ref builder,
            manager);
    }
}