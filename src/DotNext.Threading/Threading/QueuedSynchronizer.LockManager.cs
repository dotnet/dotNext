using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace DotNext.Threading;

partial class QueuedSynchronizer
{
    private protected interface ILockManager
    {
        bool IsLockAllowed { get; }

        void AcquireLock();
    }

    private protected bool TryAcquire<TLockManager>(ref TLockManager manager)
        where TLockManager : struct, ILockManager
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        if (IsEmptyQueue && manager.IsLockAllowed)
        {
            manager.AcquireLock();
            return true;
        }

        return false;
    }

    // TODO: Migrate to ref struct
    private T AcquireAsync<T, TBuilder, TNode, TLockManager>(ref TBuilder builder, ref TLockManager manager)
        where T : struct, IEquatable<T>
        where TNode : WaitNode, new()
        where TBuilder : struct, ITaskBuilder<T>
        where TLockManager : struct, ILockManager, IConsumer<TNode>
    {
        switch (builder.IsCompleted)
        {
            case true:
                goto default;
            case false when Acquire<T, TBuilder, TNode>(ref builder, TryAcquire(ref manager)) is { } node:
                manager.Invoke(node);
                goto default;
            default:
                builder.Dispose();
                break;
        }

        return builder.Invoke();
    }

    private protected ValueTask AcquireAsync<TNode, TLockManager>(ref TLockManager manager, TimeSpan timeout, CancellationToken token)
        where TNode : WaitNode, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>
    {
        var builder = CreateTaskBuilder(timeout, token);
        return AcquireAsync<ValueTask, TimeoutAndCancellationToken, TNode, TLockManager>(ref builder, ref manager);
    }

    private protected ValueTask AcquireAsync<TNode, TLockManager>(ref TLockManager manager, CancellationToken token)
        where TNode : WaitNode, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>
    {
        var builder = CreateTaskBuilder(token);
        return AcquireAsync<ValueTask, CancellationTokenOnly, TNode, TLockManager>(ref builder, ref manager);
    }

    private protected ValueTask AcquireAsync<TNode, TLockManager>(
        object? interruptionReason,
        ref TLockManager manager,
        TimeSpan timeout,
        CancellationToken token)
        where TNode : WaitNode, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>
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
            ref manager);
    }

    private protected ValueTask AcquireAsync<TNode, TLockManager>(
        object? interruptionReason,
        ref TLockManager manager,
        CancellationToken token)
        where TNode : WaitNode, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>
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
            ref manager);
    }

    private protected ValueTask<bool> TryAcquireAsync<TNode, TLockManager>(
        ref TLockManager manager,
        TimeSpan timeout,
        CancellationToken token)
        where TNode : WaitNode, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>
    {
        var builder = CreateTaskBuilder(timeout, token);
        return AcquireAsync<ValueTask<bool>, TimeoutAndCancellationToken, TNode, TLockManager>(ref builder, ref manager);
    }

    private protected ValueTask<bool> TryAcquireAsync<TNode, TLockManager>(
        object? interruptionReason,
        ref TLockManager manager,
        TimeSpan timeout,
        CancellationToken token)
        where TNode : WaitNode, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>
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
            ref manager);
    }
}