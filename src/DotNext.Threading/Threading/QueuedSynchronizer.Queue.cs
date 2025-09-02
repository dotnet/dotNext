using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Tasks;
using Tasks.Pooling;

partial class QueuedSynchronizer
{
    /// <summary>
    /// The default lock manager with no state.
    /// </summary>
    private protected static DefaultLockManager<DefaultWaitNode> DefaultManager;
    
    private WaitQueue waitQueue;
    
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private protected bool IsEmptyQueue => waitQueue.Head is null;
    
    private protected void ReturnNode<TNode>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool, TNode node)
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, new()
    {
        lock (SyncRoot)
        {
            if (node.NeedsRemoval)
                waitQueue.Remove(node);

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
            suspendedCallers = node.NeedsRemoval && waitQueue.Remove(node)
                ? DrainWaitQueue<TNode, TVisitor>(ref visitor)
                : null;

            pool.Return(node);
        }

        suspendedCallers?.Unwind();
    }

    private protected LinkedValueTaskCompletionSource<bool>? DrainWaitQueue<TNode, TVisitor>(ref TVisitor visitor)
        where TNode : WaitNode
        where TVisitor : struct, IWaitQueueVisitor<TNode>
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        var detachedQueue = new LinkedValueTaskCompletionSource<bool>.LinkedList();
        for (TNode? current = Unsafe.As<TNode>(waitQueue.Head), next; current is not null; current = next)
        {
            Debug.Assert(current.Next is null or TNode);

            next = Unsafe.As<TNode>(current.Next);

            if (!visitor.Visit(current, ref waitQueue, ref detachedQueue))
                break;
        }

        return detachedQueue.First;
    }

    private LinkedValueTaskCompletionSource<bool>? DrainWaitQueue(Exception e)
    {
        var visitor = new WaitQueueInterruptingVisitor(e);
        return DrainWaitQueue<WaitNode, WaitQueueInterruptingVisitor>(ref visitor);
    }

    private TNode EnqueueNode<T, TNode, TInitializer>(ref ValueTaskPool<bool, TNode, Action<TNode>> pool, ref TInitializer initializer)
        where T : struct, IEquatable<T>
        where TNode : WaitNode, IPooledManualResetCompletionSource<Action<TNode>>, IValueTaskFactory<T>, new()
        where TInitializer : struct, IConsumer<TNode>
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        var node = pool.Get();
        initializer.Invoke(node);
        node.Initialize(CaptureCallerInformation(), TNode.ThrowOnTimeout);
        waitQueue.Add(node);
        return node;
    }

    private protected ISupplier<TimeSpan, CancellationToken, ValueTask<bool>> EnqueueNode(
        ref ValueTaskPool<bool, DefaultWaitNode, Action<DefaultWaitNode>> pool)
        => EnqueueNode<ValueTask, DefaultWaitNode, DefaultLockManager<DefaultWaitNode>>(ref pool, ref DefaultManager);

    private protected ISupplier<TimeSpan, CancellationToken, ValueTask> EnqueueNodeThrowOnTimeout(
        ref ValueTaskPool<bool, DefaultWaitNode, Action<DefaultWaitNode>> pool)
        => EnqueueNode<ValueTask<bool>, DefaultWaitNode, DefaultLockManager<DefaultWaitNode>>(ref pool, ref DefaultManager);

    private protected bool TryAcquire<TLockManager>(ref TLockManager manager)
        where TLockManager : struct, ILockManager
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        return waitQueue.TryAcquire(ref manager);
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

        switch (options.Timeout.Ticks)
        {
            case Timeout.InfiniteTicks:
                goto default;
            case < 0L or > Timeout.MaxTimeoutParameterTicks:
                task = TNode.FromException(new ArgumentOutOfRangeException("timeout"));
                break;
            case 0L: // attempt to acquire synchronously
                LinkedValueTaskCompletionSource<bool>? interruptedCallers;
                lock (SyncRoot)
                {
                    if (IsDisposingOrDisposed)
                    {
                        task = TNode.FromException(CreateObjectDisposedException());
                        break;
                    }

                    interruptedCallers = TOptions.InterruptionRequired
                        ? Interrupt(options.InterruptionReason)
                        : null;

                    task = TryAcquire(ref manager)
                        ? TNode.SuccessfulTask
                        : TNode.TimedOutTask;
                }

                interruptedCallers?.Unwind();
                break;
            default:
                if (options.Token.IsCancellationRequested)
                {
                    task = TNode.FromCanceled(options.Token);
                    break;
                }

                ISupplier<TimeSpan, CancellationToken, T> factory;
                lock (SyncRoot)
                {
                    if (IsDisposingOrDisposed)
                    {
                        task = TNode.FromException(CreateObjectDisposedException());
                        break;
                    }

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

                interruptedCallers?.Unwind();
                task = factory.Invoke(options.Timeout, options.Token);
                break;
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
    
    [StructLayout(LayoutKind.Auto)]
    private struct WaitQueue : IWaitQueue
    {
        internal TagList MeasurementTags;
        private LinkedValueTaskCompletionSource<bool>.LinkedList list;

        public bool TryAcquire<TLockManager>(ref TLockManager manager)
            where TLockManager : struct, ILockManager
        {
            if (TLockManager.RequiresEmptyQueue && Head is not null || !manager.IsLockAllowed)
                return false;

            manager.AcquireLock();
            return true;
        }

        private bool RemoveAndSignal(LinkedValueTaskCompletionSource<bool> node,
            ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue,
            in Result<bool> result)
        {
            bool signaled;
            if (!Remove(node))
            {
                signaled = false;
            }
            else if ((signaled = node.TrySetResult(Sentinel.Instance, completionToken: null, result, out var resumable)) && resumable)
            {
                detachedQueue.Add(node);
            }

            return signaled;
        }

        bool IWaitQueue.RemoveAndSignal(LinkedValueTaskCompletionSource<bool> node,
            ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue)
            => RemoveAndSignal(node, ref detachedQueue, result: true);

        bool IWaitQueue.RemoveAndSignal(LinkedValueTaskCompletionSource<bool> node,
            ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue,
            Exception e)
            => RemoveAndSignal(node, ref detachedQueue, new(e));

        public bool Remove(LinkedValueTaskCompletionSource<bool> node)
        {
            bool removed;
            if (removed = list.Remove(node))
            {
                SuspendedCallersMeter.Add(-1, MeasurementTags);
            }

            return removed;
        }

        public void Add(WaitNode node)
        {
            list.Add(node);
            SuspendedCallersMeter.Add(1, MeasurementTags);
        }

        public readonly LinkedValueTaskCompletionSource<bool>? Head => list.First;
    }

    private protected interface IWaitQueue
    {
        bool RemoveAndSignal(LinkedValueTaskCompletionSource<bool> node, ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue);

        bool RemoveAndSignal(LinkedValueTaskCompletionSource<bool> node, ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue,
            Exception e);
    }

    private protected interface IWaitQueueVisitor<in TNode>
        where TNode : WaitNode
    {
        bool Visit<TWaitQueue>(TNode node, ref TWaitQueue queue, ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue)
            where TWaitQueue : IWaitQueue;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct WaitQueueInterruptingVisitor(Exception exception) : IWaitQueueVisitor<WaitNode>
    {
        bool IWaitQueueVisitor<WaitNode>.Visit<TWaitQueue>(WaitNode node, ref TWaitQueue queue, ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue)
        {
            queue.RemoveAndSignal(node, ref detachedQueue, exception);
            return true;
        }
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