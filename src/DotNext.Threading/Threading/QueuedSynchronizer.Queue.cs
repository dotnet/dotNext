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

    private protected virtual void DrainWaitQueue(ref WaitQueueScope queue)
    {
    }

    private void ReturnNode(WaitNode node)
    {
        if (node.NeedsRemoval)
        {
            RemoveAndDrainIfNeeded(node);
        }

        node.Reset();

        // the node is removed for sure, it can be returned back to the pool
        if (!IsDisposingOrDisposed)
        {
            BackToPool(node);
        }
    }

    private void BackToPool(WaitNode node)
    {
        lock (waitQueue.SyncRoot)
        {
            pool.Return(node);
        }
    }

    private void RemoveAndDrainIfNeeded(WaitNode node)
    {
        WaitQueueScope scope;
        lock (waitQueue.SyncRoot)
        {
            if (waitQueue.Remove(node))
            {
                scope = new(ref waitQueue);
                DrainWaitQueue(ref scope);
            }
            else
            {
                scope = default;
            }
        }

        scope.ResumeSuspendedCallers();
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

    /// <summary>
    /// Captures the wait queue and acquires the internal lock.
    /// </summary>
    /// <remarks>
    /// The internal lock must be released with <see cref="WaitQueueScope.Dispose()"/>
    /// </remarks>
    /// <returns>The captured wait queue.</returns>
    private protected WaitQueueScope CaptureWaitQueue()
    {
        waitQueue.SyncRoot.Enter();
        return new(ref waitQueue);
    }

    [StructLayout(LayoutKind.Auto)]
    private protected ref struct WaitQueueScope : IDisposable
    {
        private readonly ref WaitQueue queue;
        private LinkedValueTaskCompletionSource<bool>.LinkedList detachedQueue;
        private LinkedValueTaskCompletionSource<bool>? current, next;

        public WaitQueueScope(ref WaitQueue queue)
        {
            this.queue = ref queue;
            next = (current = queue.First)?.Next;
        }

        public readonly void ResumeSuspendedCallers()
            => detachedQueue.First?.Unwind();

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

        public void SignalAll(Exception e) => SignalAll(new Result<bool>(e));

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

        /// <summary>
        /// Releases the internal lock and resumes the suspended callers.
        /// </summary>
        /// <remarks>
        /// Must not be called if this object wasn't constructed with <see cref="QueuedSynchronizer.CaptureWaitQueue"/>.
        /// </remarks>
        public readonly void Dispose()
        {
            queue.SyncRoot.Exit();
            ResumeSuspendedCallers();
        }
    }
    
    [StructLayout(LayoutKind.Auto)]
    private protected struct WaitQueue()
    {
        internal readonly System.Threading.Lock SyncRoot = new();
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
            lock (SyncRoot)
            {
                return GetSuspendedCallersCore();
            }
        }

        private readonly IReadOnlyList<object?> GetSuspendedCallersCore()
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
}