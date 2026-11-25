using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Threading;

using Tasks;

partial class AsyncCoalescer
{
    // Normally, the only batch is in use. More batches are needed if the windows are closed faster
    // than the actions complete
    private static readonly int MaxRetainedBatches = Environment.ProcessorCount * 2 + 1;

    private Batch? current; // the batch of the open window, null if the window is closed
    private Batch? batchPool; // intrusive stack of the reusable batches
    private int batchPoolSize;
    
    private Batch RentBatch()
    {
        Debug.Assert(syncRoot.IsHeldByCurrentThread);

        Batch result;
        if (batchPool is null)
        {
            result = new();
        }
        else
        {
            result = batchPool;
            batchPool = result.NextInPool;
            result.NextInPool = null;
            batchPoolSize--;
        }

        return result;
    }

    // Called by the batch when the action is completed. The batch must not be accessed after this call,
    // because it can be rented by another window
    private void ResumeAndReturn(Batch batch, Result<bool> result)
    {
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (syncRoot)
        {
            suspendedCallers = batch.Drain(in result);

            // The drained batch can be reused: the canceled nodes still referencing it are detached,
            // so their later removal is no-op
            if (!disposed && batchPoolSize < MaxRetainedBatches)
            {
                batch.NextInPool = batchPool;
                batchPool = batch;
                batchPoolSize++;
            }
        }

        suspendedCallers?.Unwind();
    }
    
    // The suspended callers of the single window, and the invocation of the action for them.
    // The queue must be accessed under the lock
    private sealed class Batch : IThreadPoolWorkItem
    {
        private readonly Action resumeCallback;
        private LinkedValueTaskCompletionSource<bool>.LinkedList queue;
        private AsyncCoalescer? owner;
        private bool triggeredManually;
        private ConfiguredTaskAwaitable.ConfiguredTaskAwaiter awaiter;

        internal Batch? NextInPool; // guarded by the lock

        internal Batch() => resumeCallback = Resume;

        internal void Start(AsyncCoalescer owner, bool triggeredManually)
        {
            this.owner = owner;
            this.triggeredManually = triggeredManually;
            ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: false);
        }

        void IThreadPoolWorkItem.Execute()
        {
            Debug.Assert(owner is not null);

            Task task;
            try
            {
                task = owner.CompleteAsync(triggeredManually);
            }
            catch (Exception e)
            {
                task = Task.FromException(e);
            }

            awaiter = task.ConfigureAwait(false).GetAwaiter();
            if (awaiter.IsCompleted)
            {
                Resume();
            }
            else
            {
                awaiter.UnsafeOnCompleted(resumeCallback);
            }
        }

        private void Resume()
        {
            Debug.Assert(owner is not null);

            var result = new Result<bool>(triggeredManually);
            try
            {
                awaiter.GetResult();
            }
            catch (Exception e)
            {
                result = new(e);
            }

            // clean up the state before returning the batch to the pool
            var coalescer = owner;
            owner = null;
            awaiter = default;
            coalescer.ResumeAndReturn(this, result);
        }

        internal void Add(WaitNode node) => queue.Add(node);

        internal void Remove(WaitNode node) => queue.Remove(node);

        // The queue is reset, so the later removal of the canceled node is no-op
        internal LinkedValueTaskCompletionSource<bool>? Drain(in Result<bool> result)
        {
            var first = queue.First;
            queue = default;
            return first?.SetResult(result);
        }
    }
}