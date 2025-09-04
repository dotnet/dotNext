using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Patterns;
using Tasks;
using Tasks.Pooling;

partial class QueuedSynchronizer
{
    private LinkedValueTaskCompletionSource<bool>.LinkedList waitQueue;
    
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private protected bool IsEmptyQueue => waitQueue.First is null;
    
    private protected void ReturnNode<TNode>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool, TNode node)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
    {
        lock (SyncRoot)
        {
            if (node.NeedsRemoval)
                RemoveNode(node);

            pool.Return(node);
        }
    }

    private protected void ReturnNode<TNode, TVisitor>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool, TNode node, ref TVisitor visitor)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TVisitor : struct, IWaitQueueVisitor<TNode>
    {
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            suspendedCallers = node.NeedsRemoval && RemoveNode(node)
                ? DrainWaitQueue<TNode, TVisitor>(ref visitor)
                : null;

            pool.Return(node);
        }

        suspendedCallers?.Unwind();
    }

    private protected unsafe void ReturnNode<TOwner, TNode>(
        ref ValueTaskPool<bool, TNode, Action<TNode>> pool,
        TNode node,
        delegate*<TOwner, TNode, out bool, bool> visitor)
        where TOwner : QueuedSynchronizer
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        => ReturnNode(ref pool, node, ref DelegatingVisitor<TNode>.Create(visitor));

    private protected LinkedValueTaskCompletionSource<bool>? DrainWaitQueue<TNode, TVisitor>(ref TVisitor visitor)
        where TNode : WaitNode
        where TVisitor : struct, IWaitQueueVisitor<TNode>
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        var detachedQueue = new LinkedValueTaskCompletionSource<bool>.LinkedList();
        for (var current = Unsafe.As<TNode>(waitQueue.First); current is not null;)
        {
            Debug.Assert(current.Next is null or TNode);

            var next = Unsafe.As<TNode>(current.Next);

            var iterate = visitor.Visit(current, out var resumable);
            if (current.IsCompleted)
            {
                RemoveNode(current);

                if (resumable)
                {
                    detachedQueue.Add(current);
                }
            }

            current = iterate ? next : null;
        }

        return detachedQueue.First;
    }

    private protected unsafe LinkedValueTaskCompletionSource<bool>? DrainWaitQueue<TOwner, TNode>(delegate*<TOwner, TNode, out bool, bool> visitor)
        where TOwner : QueuedSynchronizer
        where TNode : WaitNode, ISupplier<MulticastDelegate?>
    {
        return DrainWaitQueue<TNode, DelegatingVisitor<TNode>>(ref DelegatingVisitor<TNode>.Create(visitor));
    }

    private LinkedValueTaskCompletionSource<bool>? DrainWaitQueue(Exception e)
    {
        var visitor = new ResumingVisitor(e);
        return DrainWaitQueue<WaitNode, ResumingVisitor>(ref visitor);
    }

    private ISupplier<TimeSpan, CancellationToken, T> EnqueueNode<T, TNode, TInitializer>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool, ref TInitializer initializer)
        where T : struct, IEquatable<T>
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, IValueTaskFactory<T>, new()
        where TInitializer : struct, IConsumer<TNode>
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        var node = pool.Get();
        initializer.Invoke(node);
        node.Initialize(CaptureCallerInformation(), TNode.ThrowOnTimeout);
        EnqueueNode(node);
        return node;
    }

    private protected ISupplier<TimeSpan, CancellationToken, ValueTask<bool>> EnqueueNode(
        ref ValueTaskPool<bool, DefaultWaitNode, Action<DefaultWaitNode>> pool)
        => EnqueueNode<ValueTask<bool>, DefaultWaitNode, DefaultLockManager<DefaultWaitNode>>(ref pool, ref DefaultManager);

    private protected ISupplier<TimeSpan, CancellationToken, ValueTask> EnqueueNodeThrowOnTimeout(
        ref ValueTaskPool<bool, DefaultWaitNode, Action<DefaultWaitNode>> pool)
        => EnqueueNode<ValueTask, DefaultWaitNode, DefaultLockManager<DefaultWaitNode>>(ref pool, ref DefaultManager);

    private protected bool TryAcquire<TLockManager>(ref TLockManager manager)
        where TLockManager : struct, ILockManager
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        if (TLockManager.RequiresEmptyQueue && !IsEmptyQueue || !manager.IsLockAllowed)
            return false;

        manager.AcquireLock();
        return true;
    }

    private T AcquireAsync<T, TNode, TLockManager, TOptions>(
        ref ValueTaskPool<bool, TNode, Action<TNode>> pool,
        ref TLockManager manager,
        TOptions options)
        where T : struct, IEquatable<T>
        where TNode : WaitNode, IValueTaskFactory<T>, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>
        where TOptions : struct, IAcquisitionOptions
    {
        T task;

        if (options.Token.IsCancellationRequested)
        {
            task = TNode.FromCanceled(options.Token);
        }
        else if (IsDisposingOrDisposed)
        {
            task = TNode.FromException(CreateObjectDisposedException());
        }
        else
        {
            LinkedValueTaskCompletionSource<bool>? interruptedCallers;
            switch (options.Timeout.Ticks)
            {
                case Timeout.InfiniteTicks:
                    goto default;
                case < 0L or > Timeout.MaxTimeoutParameterTicks:
                    task = TNode.FromException(new ArgumentOutOfRangeException("timeout"));
                    interruptedCallers = null;
                    break;
                case 0L: // attempt to acquire synchronously
                    lock (SyncRoot)
                    {
                        interruptedCallers = TOptions.InterruptionRequired
                            ? Interrupt(options.InterruptionReason)
                            : null;

                        task = TryAcquire(ref manager)
                            ? TNode.SuccessfulTask
                            : TNode.TimedOutTask;
                    }

                    break;
                default:
                    ISupplier<TimeSpan, CancellationToken, T> factory;
                    lock (SyncRoot)
                    {
                        interruptedCallers = TOptions.InterruptionRequired
                            ? Interrupt(options.InterruptionReason)
                            : null;

                        if (TryAcquire(ref manager))
                        {
                            task = TNode.SuccessfulTask;
                            break;
                        }

                        factory = EnqueueNode<T, TNode, TLockManager>(ref pool, ref manager);
                    }

                    task = factory.Invoke(options.Timeout, options.Token);
                    break;
            }

            interruptedCallers?.Unwind();
        }

        return task;

        ObjectDisposedException CreateObjectDisposedException()
            => new(GetType().Name);
    }

    private protected ValueTask AcquireAsync<TNode, TLockManager, TOptions>(
        ref ValueTaskPool<bool, TNode, Action<TNode>> pool,
        ref TLockManager manager,
        TOptions options)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>
        where TOptions : struct, IAcquisitionOptions
        => AcquireAsync<ValueTask, TNode, TLockManager, TOptions>(
            ref pool,
            ref manager,
            options);

    private protected ValueTask<bool> TryAcquireAsync<TNode, TLockManager, TOptions>(
        ref ValueTaskPool<bool, TNode, Action<TNode>> pool,
        ref TLockManager manager,
        TOptions options)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>
        where TOptions : struct, IAcquisitionOptionsWithTimeout
        => AcquireAsync<ValueTask<bool>, TNode, TLockManager, TOptions>(
            ref pool,
            ref manager,
            options);

    private bool RemoveNode(LinkedValueTaskCompletionSource<bool> node)
    {
        SuspendedCallersMeter.Add(-1, measurementTags);
        return waitQueue.Remove(node);
    }

    private void EnqueueNode(WaitNode node)
    {
        SuspendedCallersMeter.Add(1, measurementTags);
        waitQueue.Add(node);
    }
    
    private protected interface IWaitQueueVisitor<in TNode>
        where TNode : WaitNode
    {
        bool Visit(TNode node, out bool resumable);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct ResumingVisitor(Exception exception) : IWaitQueueVisitor<WaitNode>
    {
        bool IWaitQueueVisitor<WaitNode>.Visit(WaitNode node, out bool resumable)
        {
            node.TrySetException(exception, out resumable);
            return true;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly unsafe struct DelegatingVisitor<TNode> : IWaitQueueVisitor<TNode>
        where TNode : WaitNode, ISupplier<MulticastDelegate?>
    {
        bool IWaitQueueVisitor<TNode>.Visit(TNode node, out bool resumable)
        {
            var visitor = (delegate*<QueuedSynchronizer, TNode, out bool, bool>)Unsafe.AsPointer(ref Unsafe.AsRef(in this));

            return node.Invoke()?.Target is QueuedSynchronizer owner
                ? visitor(owner, node, out resumable)
                : node.TrySetResult(out resumable);
        }

        public static ref DelegatingVisitor<TNode> Create<TOwner>(delegate*<TOwner, TNode, out bool, bool> visitor)
            where TOwner : QueuedSynchronizer
            => ref Unsafe.AsRef<DelegatingVisitor<TNode>>(visitor);
    }
}