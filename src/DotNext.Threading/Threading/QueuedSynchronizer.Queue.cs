using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Tasks;
using Tasks.Pooling;

partial class QueuedSynchronizer
{
    private ValueTaskPool<bool> pool;
    private WaitQueue waitQueue;
    
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private protected bool IsEmptyQueue => waitQueue.First is null;

    private protected abstract void DrainWaitQueue(ref WaitQueueVisitor waitQueueVisitor);

    private protected WaitQueueVisitor GetWaitQueue(ref LinkedValueTaskCompletionSource<bool>.LinkedList suspendedCallers)
        => new(ref waitQueue, ref suspendedCallers);

    private protected LinkedValueTaskCompletionSource<bool>? DrainWaitQueue()
    {
        var detachedQueue = new LinkedValueTaskCompletionSource<bool>.LinkedList();
        var visitor = GetWaitQueue(ref detachedQueue);
        DrainWaitQueue(ref visitor);
        return detachedQueue.First;
    }

    private LinkedValueTaskCompletionSource<bool>? DrainWaitQueue(Exception e)
    {
        var detachedQueue = new LinkedValueTaskCompletionSource<bool>.LinkedList();
        GetWaitQueue(ref detachedQueue).SignalAll(e);

        return detachedQueue.First;
    }

    private void ReturnNode(WaitNode node)
    {
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            suspendedCallers = node.NeedsRemoval && waitQueue.Remove(node) && node.DrainOnReturn
                ? DrainWaitQueue()
                : null;

            pool.Return(node);
        }

        suspendedCallers?.Unwind();
    }

    private ISupplier<TimeSpan, CancellationToken, T> EnqueueNode<T, TNode, TInitializer>(ref TInitializer initializer)
        where T : struct, IEquatable<T>
        where TNode : WaitNode, IValueTaskFactory<T>, new()
        where TInitializer : struct, IConsumer<TNode>
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        var node = pool.Get<TNode>();
        initializer.Invoke(node);
        node.Initialize(this, CaptureCallerInformation(), TNode.ThrowOnTimeout);
        waitQueue.Add(node);
        return node;
    }

    private protected ISupplier<TimeSpan, CancellationToken, ValueTask<bool>> EnqueueNode()
        => EnqueueNode<ValueTask<bool>, WaitNode, DefaultLockManager<WaitNode>>(ref DefaultManager);

    private protected ISupplier<TimeSpan, CancellationToken, ValueTask> EnqueueNodeThrowOnTimeout()
        => EnqueueNode<ValueTask, WaitNode, DefaultLockManager<WaitNode>>(ref DefaultManager);

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
        ref TLockManager manager,
        TOptions options)
        where T : struct, IEquatable<T>
        where TNode : WaitNode, IValueTaskFactory<T>, new()
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

                        factory = EnqueueNode<T, TNode, TLockManager>(ref manager);
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
        ref TLockManager manager,
        TOptions options)
        where TNode : WaitNode, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>
        where TOptions : struct, IAcquisitionOptions
        => AcquireAsync<ValueTask, TNode, TLockManager, TOptions>(
            ref manager,
            options);

    private protected ValueTask<bool> TryAcquireAsync<TNode, TLockManager, TOptions>(
        ref TLockManager manager,
        TOptions options)
        where TNode : WaitNode, new()
        where TLockManager : struct, ILockManager, IConsumer<TNode>
        where TOptions : struct, IAcquisitionOptionsWithTimeout
        => AcquireAsync<ValueTask<bool>, TNode, TLockManager, TOptions>(
            ref manager,
            options);

    [StructLayout(LayoutKind.Auto)]
    private protected ref struct WaitQueueVisitor
    {
        private readonly ref WaitQueue queue;
        private readonly ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue;
        private LinkedValueTaskCompletionSource<bool>? current, next;

        public WaitQueueVisitor(ref WaitQueue queue, ref LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue)
        {
            this.queue = ref queue;
            this.detachedQueue = ref detachedQueue;
            next = (current = queue.First)?.Next;
        }

        private readonly bool EndOfQueue => current is null;

        public readonly bool IsEndOfQueue<TNode, TResult>([MaybeNullWhen(true)] out TResult result)
            where TNode : WaitNode, INodeMapper<TNode, TResult>
        {
            if (current is TNode currentNode)
            {
                result = TNode.GetValue(currentNode);
                return false;
            }

            result = default;
            return true;
        }

        public void Advance() => next = (current = next)?.Next;

        private bool SignalCurrent(in Result<bool> result)
        {
            bool signaled;
            if (current is not null)
            {
                queue.Remove(current);
                signaled = current.TrySetResult(Sentinel.Instance, completionToken: null, result, out var resumable);
                if (resumable)
                {
                    detachedQueue.Add(current);
                }
            }
            else
            {
                signaled = false;
            }

            return signaled;
        }

        public bool SignalCurrent() => SignalCurrent(result: true);

        public bool SignalCurrent<TLockManager>(ref TLockManager manager)
            where TLockManager : struct, ILockManager
        {
            if (!manager.IsLockAllowed)
                return false;

            if (SignalCurrent())
                manager.AcquireLock();

            return true;
        }

        public void SignalAll<TLockManager>(ref TLockManager manager)
            where TLockManager : struct, ILockManager
        {
            while (!EndOfQueue && SignalCurrent(ref manager))
            {
                Advance();
            }
        }

        private void SignalAll(in Result<bool> result)
        {
            while (!EndOfQueue)
            {
                SignalCurrent(in result);
                Advance();
            }
        }

        public void SignalAll() => SignalAll(new Result<bool>(true));

        public void SignalAll(Exception e) => SignalAll(new Result<bool>(e));

        private void SignalAll(in Result<bool> result, out bool signaled)
        {
            for (signaled = false; !EndOfQueue; Advance())
            {
                signaled |= SignalCurrent(in result);
            }
        }

        public void SignalAll(out bool signaled)
            => SignalAll(new Result<bool>(true), out signaled);

        public void SignalAll(Exception e, out bool signaled)
            => SignalAll(new Result<bool>(e), out signaled);

        private void SignalFirst(in Result<bool> result, out bool signaled)
        {
            for (signaled = false; !EndOfQueue; Advance())
            {
                if (SignalCurrent(in result))
                {
                    signaled = true;
                    break;
                }
            }
        }
        
        public void SignalFirst(out bool signaled)
            => SignalFirst(new Result<bool>(true), out signaled);
    }
    
    [StructLayout(LayoutKind.Auto)]
    private protected struct WaitQueue
    {
        private readonly TagList measurementTags;
        private LinkedValueTaskCompletionSource<bool>.LinkedList waitQueue;

        public TagList MeasurementTags
        {
            init => measurementTags = value;
        }

        public readonly LinkedValueTaskCompletionSource<bool>? First => waitQueue.First;
        
        public bool Remove(LinkedValueTaskCompletionSource<bool> node)
        {
            SuspendedCallersMeter.Add(-1, measurementTags);
            return waitQueue.Remove(node);
        }

        public void Add(LinkedValueTaskCompletionSource<bool> node)
        {
            SuspendedCallersMeter.Add(1, measurementTags);
            waitQueue.Add(node);
        }
    }
}