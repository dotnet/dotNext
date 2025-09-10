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

    private protected TNode? Acquire<T, TBuilder, TNode>(ref TBuilder builder, bool acquired)
        where T : struct, IEquatable<T>
        where TNode : WaitNode, new()
        where TBuilder : struct, ITaskBuilder<T>
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));
        Debug.Assert(!builder.IsCompleted);

        if (IsDisposingOrDisposed)
        {
            builder.CompleteAsDisposed(GetType().Name);
        }
        else if (acquired)
        {
            builder.Complete();
        }
        else if (builder.IsTimedOut)
        {
            builder.CompleteAsTimedOut();
        }
        else if (IsConcurrencyLimitReached)
        {
            builder.CompletedAsFull();
        }
        else
        {
            var node = pool.Get<TNode>();
            node.Initialize(this, CaptureCallerInformation(), TBuilder.ThrowOnTimeout);
            waitQueue.Add(node);
            builder.Complete(node);
            return node;
        }

        return null;
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

        public IReadOnlyList<object?> GetSuspendedCallers()
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
                    result[index++] = current.CallerInfo;
                    current = current.Next as WaitNode;
                }
            }

            return result;
        }
    }
}