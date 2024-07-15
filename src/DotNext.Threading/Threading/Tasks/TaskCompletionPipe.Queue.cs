using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks;

public partial class TaskCompletionPipe<T>
{
    private class LinkedTaskNode
    {
        internal readonly T Task;
        private object? nextOrOwner;

        internal LinkedTaskNode(T task) => Task = task;

        private protected LinkedTaskNode(T task, TaskCompletionPipe<T> owner)
        {
            Debug.Assert(task is not null);
            Debug.Assert(owner is not null);

            Task = task;
            nextOrOwner = owner;
        }

        internal object? UserData
        {
            get;
            init;
        }

        internal LinkedTaskNode? Next
        {
            get
            {
                Debug.Assert(nextOrOwner is not TaskCompletionPipe<T>);

                return Unsafe.As<LinkedTaskNode>(nextOrOwner);
            }

            set => nextOrOwner = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected TaskCompletionPipe<T>? GetOwnerAndClear()
        {
            Debug.Assert(nextOrOwner is not LinkedTaskNode);

            var result = Unsafe.As<TaskCompletionPipe<T>>(nextOrOwner);
            nextOrOwner = null;
            return result;
        }
    }

    private sealed class LazyLinkedTaskNode : LinkedTaskNode
    {
        private readonly uint expectedVersion;

        internal LazyLinkedTaskNode(T task, TaskCompletionPipe<T> owner, uint version)
            : base(task, owner)
            => expectedVersion = version;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void Invoke()
        {
            if (GetOwnerAndClear() is { } owner && owner.Version == expectedVersion)
                owner.EnqueueCompletedTask(this, expectedVersion);
        }
    }

    private LinkedTaskNode? firstTask, lastTask;

    private void AddCompletedTaskNode(LinkedTaskNode node)
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));
        Debug.Assert(node.Task.IsCompleted);

        if (lastTask is null)
        {
            firstTask = lastTask = node;
        }
        else
        {
            lastTask = lastTask.Next = node;
        }
    }

    private ManualResetCompletionSource? EnqueueCompletedTask(LinkedTaskNode node)
    {
        AddCompletedTaskNode(node);

        if (--scheduledTasksCount is 0U && completionRequested && completedAll is not null)
            completedAll.TrySetResult();

        // Detaches continuation to call later out of monitor lock.
        // This approach increases response time (the time needed to submit completed task asynchronously),
        // but also improves throughput (number of submitted tasks per second).
        // Typically, the pipe has single consumer and multiple producers. In that
        // case, improved throughput is most preferred.
        return TryDetachSuspendedCaller();
    }

    private LinkedValueTaskCompletionSource<bool>? TryDetachSuspendedCaller()
    {
        for (LinkedValueTaskCompletionSource<bool>? current = waitQueue.First, next; current is not null; current = next)
        {
            next = current.Next;
            waitQueue.Remove(current);
            if (current.TrySetResult(Sentinel.Instance, completionToken: null, true, out var resumable) && resumable)
                return current;
        }

        return null;
    }

    private void EnqueueCompletedTask(LinkedTaskNode node, uint expectedVersion)
    {
        ManualResetCompletionSource? suspendedCaller;
        lock (SyncRoot)
        {
            suspendedCaller = version == expectedVersion
                ? EnqueueCompletedTask(node)
                : null;
        }

        suspendedCaller?.Resume();
    }

    private bool TryDequeueCompletedTask([NotNullWhen(true)] out T? task, out object? userData)
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        if (firstTask is not null)
        {
            task = firstTask.Task;
            userData = firstTask.UserData;
            var next = firstTask.Next;
            firstTask.Next = null; // help GC
            if ((firstTask = next) is null)
                lastTask = null;

            return true;
        }

        userData = task = null;
        return false;
    }

    private void ClearTaskQueue()
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        firstTask = lastTask = null;
    }
}