using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Tasks;

partial class AsyncStateTracker
{
    private readonly System.Threading.Lock syncRoot;
    private WaitNode.LinkedList waitQueue;
    
    private void ReturnNode(WaitNode node)
    {
        if (node.NeedsRemoval)
        {
            RemoveNode(node);
        }

        node.Reset();

        // return to the pool
        if (!IsCompleted)
        {
            ReturnToPool(node);
        }
    }
    
    private void RemoveNode(WaitNode node)
    {
        lock (syncRoot)
        {
            waitQueue.Remove(node);
        }
    }
    
    private bool DrainWaitQueue<TStrategy>(out bool resumed)
        where TStrategy : struct, ICompletionStrategy, allows ref struct
    {
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        bool result;
        if (completed)
        {
            suspendedCallers = null;
            result = false;
        }
        else
        {
            lock (syncRoot)
            {
                if (completed)
                {
                    suspendedCallers = null;
                    result = false;
                }
                else
                {
                    Advance<TStrategy>();
                    suspendedCallers = DrainWaitQueue<TStrategy>();
                    result = true;
                }
            }
        }

        if (suspendedCallers is not null)
        {
            resumed = true;
            suspendedCallers.Unwind();
        }
        else
        {
            resumed = false;
        }
        
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Advance<TStrategy>()
        where TStrategy : struct, ICompletionStrategy, allows ref struct
    {
        if (TStrategy.Result)
        {
            ChangeVersion(ref currentVersion);
        }
        else
        {
            completed = true;
            pool.Reset(); // after completion, nothing can be reused from the pool
        }
    }

    private LinkedValueTaskCompletionSource<bool>? DrainWaitQueue<TStrategy>()
        where TStrategy : struct, ICompletionStrategy, allows ref struct
    {
        Debug.Assert(syncRoot.IsHeldByCurrentThread);
        
        var detachedQueue = default(WaitNode.LinkedList);
        for (WaitNode? current = Unsafe.As<WaitNode>(waitQueue.First), next; current is not null && TStrategy.CheckVersion(current, currentVersion); current = next)
        {
            next = current.Next;

            if (current.TrySetResult(TStrategy.Result, out var resumable))
            {
                waitQueue.Remove(current);

                if (resumable)
                {
                    detachedQueue.Add(current);
                }
            }
        }

        return detachedQueue.First;
    }

    private ISupplier<TimeSpan, CancellationToken, ValueTask<bool>> EnqueueNode(ulong nodeVersion)
    {
        Debug.Assert(syncRoot.IsHeldByCurrentThread);

        var node = pool.Rent<WaitNode>();
        node.Initialize(this, nodeVersion);
        waitQueue.Add(node);
        return node;
    }
    
    private sealed class WaitNode : LinkedValueTaskCompletionSource<bool>
    {
        private AsyncStateTracker? owner;

        public void Initialize(AsyncStateTracker tracker, ulong expectedVersion)
        {
            Debug.Assert(tracker is not null);

            owner = tracker;
            ExpectedVersion = expectedVersion;
        }

        public ulong ExpectedVersion { get; private set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool NeedsRemoval => CompletionData is null;

        public new WaitNode? Next => Unsafe.As<WaitNode>(base.Next);

        public bool TrySetResult(bool result, out bool resumable)
            => base.TrySetResult(new CustomCompletionData(Sentinel.Instance), new Result<bool>.Ok(result), out resumable);

        protected override void CleanUp()
        {
            owner = null;
            base.CleanUp();
        }

        protected override void AfterConsumed() => owner?.ReturnNode(this);
    }

    private interface ICompletionStrategy
    {
        public static abstract bool CheckVersion(WaitNode node, ulong currentVersion);
        public static abstract bool Result { get; }
    }
    
    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct CompletionStrategy : ICompletionStrategy
    {
        static bool ICompletionStrategy.CheckVersion(WaitNode node, ulong currentVersion) => true;

        static bool ICompletionStrategy.Result => false;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct ResumeOldNodesStrategy : ICompletionStrategy
    {
        static bool ICompletionStrategy.CheckVersion(WaitNode node, ulong currentVersion)
            => node.ExpectedVersion <= currentVersion;

        static bool ICompletionStrategy.Result => true;
    }
    
    private static void ChangeVersion(ref ulong currentVersion)
    {
        if (nuint.Size is sizeof(ulong))
        {
            currentVersion += 1UL;
        }
        else
        {
            ref var truncated = ref Unsafe.As<ulong, uint>(ref currentVersion);
            truncated += 1U;
        }
    }
}