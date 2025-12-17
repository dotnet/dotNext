using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Tasks;
using Tasks.Pooling;

partial class QueuedSynchronizer
{
    private readonly bool concurrencyLimited;
    private ValueTaskPool<bool> pool;
    private WaitQueue waitQueue;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private protected bool IsEmptyQueue => waitQueue.Length is 0L;

    private protected abstract void DrainWaitQueue(ref WaitQueueVisitor waitQueueVisitor);

    private protected LinkedValueTaskCompletionSource<bool>? DrainWaitQueue()
    {
        AssertInternalLockState();
        
        var detachedQueue = new LinkedValueTaskCompletionSource<bool>.LinkedList();
        var visitor = new WaitQueueVisitor(ref waitQueue, ref detachedQueue);
        DrainWaitQueue(ref visitor);
        return detachedQueue.First;
    }

    private protected bool DrainWaitQueue<TVisitor>(scoped TVisitor visitor, out LinkedValueTaskCompletionSource<bool>? suspendedCallers)
        where TVisitor : struct, IWaitQueueVisitor, allows ref struct
    {
        var detachedQueue = new LinkedValueTaskCompletionSource<bool>.LinkedList();
        var waitQueue = new WaitQueueVisitor(ref this.waitQueue, ref detachedQueue);
        var signaled = visitor.Visit(ref waitQueue);
        suspendedCallers = detachedQueue.First;
        return signaled;
    }

    private void ReturnNode(WaitNode node)
    {
        if (node.NeedsRemoval)
        {
            RemoveAndDrainIfNeeded(node);
        }

        // the node is removed for sure, it can be returned back to the pool
        if (node.TryReset(out _) && !IsDisposingOrDisposed)
        {
            BackToPool(node);
        }
    }

    private void BackToPool(WaitNode node)
    {
        lock (syncRoot)
        {
            pool.Return(node);
        }
    }

    private void RemoveAndDrainIfNeeded(WaitNode node)
    {
        var suspendedCallers = default(LinkedValueTaskCompletionSource<bool>);
        syncRoot.Enter();
        try
        {
            suspendedCallers = waitQueue.Remove(node) && node.DrainOnReturn
                ? DrainWaitQueue()
                : null;
        }
        finally
        {
            syncRoot.Exit();
            suspendedCallers?.Unwind();
        }
    }

    private protected TNode? Acquire<T, TBuilder, TNode>(ref TBuilder builder, bool acquired)
        where T : struct, IEquatable<T>
        where TNode : WaitNode, new()
        where TBuilder : struct, ITaskBuilder<T>, allows ref struct
    {
        AssertInternalLockState();
        Debug.Assert(!builder.IsCompleted);

        var node = default(TNode);
        if (IsDisposingOrDisposed)
        {
            builder.CompleteAsDisposed(GetType().Name);
        }
        else if (acquired)
        {
            builder.Complete();
        }
        else if (builder.TryCompleteAsTimedOut())
        {
            // nothing to do
        }
        else if (IsConcurrencyLimitReached)
        {
            builder.Complete<ConcurrencyLimitReachedExceptionFactory>();
        }
        else
        {
            node = pool.Rent<TNode>();
            node.Initialize(this, CaptureCallerInformation(), TBuilder.ThrowOnTimeout);
            waitQueue.Add(node);
            builder.Complete(node);
        }

        return node;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool IsConcurrencyLimitReached
    {
        get
        {
            var length = waitQueue.Length;
            return length is long.MaxValue || (concurrencyLimited && length == pool.MaximumRetained);
        }
    }

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
            if (current is null)
            {
                signaled = false;
            }
            else if (signaled = current.TrySetResult(Sentinel.Instance, completionToken: null, result, out var resumable))
            {
                // Remove here only if the node is signaled by the visitor.
                // Otherwise, the node is signaled by the timeout or cancellation token
                queue.Remove(current);

                if (resumable)
                {
                    detachedQueue.Add(current);
                }
            }

            return signaled;
        }

        public bool SignalCurrent() => SignalCurrent(result: true);

        public void SignalCurrent(Exception e) => SignalCurrent(result: new(e));

        public bool SignalCurrent<TLockManager>(TLockManager manager)
            where TLockManager : struct, ILockManager, allows ref struct
        {
            if (!manager.IsLockAllowed)
                return false;

            if (SignalCurrent())
                manager.AcquireLock();

            return true;
        }

        public void SignalAll<TLockManager>(TLockManager manager)
            where TLockManager : struct, ILockManager, allows ref struct
        {
            while (!EndOfQueue && SignalCurrent(manager))
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
        private long length;

        public TagList MeasurementTags
        {
            init => measurementTags = value;
        }

        public readonly long Length => length;

        public readonly LinkedValueTaskCompletionSource<bool>? First => waitQueue.First;

        public bool Remove(LinkedValueTaskCompletionSource<bool> node)
        {
            SuspendedCallersMeter.Add(-1, measurementTags);
            length--;

            Debug.Assert(length >= 0L);
            return waitQueue.Remove(node);
        }

        public void Add(LinkedValueTaskCompletionSource<bool> node)
        {
            SuspendedCallersMeter.Add(1, measurementTags);
            waitQueue.Add(node);
            length++;
        }

        public readonly IReadOnlyList<object?> GetSuspendedCallers()
        {
            object?[] result;
            var current = waitQueue.First as WaitNode;
            if (current is null)
            {
                result = [];
            }
            else
            {
                result = new object?[length];
                for (var index = 0L; current is not null && index < length; index++)
                {
                    result[index] = current.CallerInfo;
                    current = current.Next as WaitNode;
                }
            }

            return result;
        }
    }
    
    private protected interface IWaitQueueVisitor
    {
        bool Visit(scoped ref WaitQueueVisitor waitQueueVisitor);
    }
    
    [StructLayout(LayoutKind.Auto)]
    private protected readonly ref struct ExceptionVisitor(Exception e) : IWaitQueueVisitor
    {
        bool IWaitQueueVisitor.Visit(scoped ref WaitQueueVisitor waitQueueVisitor)
        {
            waitQueueVisitor.SignalAll(e, out var signaled);
            return signaled;
        }
    }
}