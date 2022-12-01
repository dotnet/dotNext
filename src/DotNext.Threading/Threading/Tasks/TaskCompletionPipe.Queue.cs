using System.Diagnostics.CodeAnalysis;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks;

public partial class TaskCompletionPipe<T>
{
    private class LinkedTaskNode
    {
        internal readonly T Task;
        internal LinkedTaskNode? Next;

        internal LinkedTaskNode(T task) => Task = task;
    }

    private sealed class LazyLinkedTaskNode : LinkedTaskNode
    {
        private readonly uint expectedVersion;
        private TaskCompletionPipe<T>? owner;

        internal LazyLinkedTaskNode(T task, TaskCompletionPipe<T> owner, uint version)
            : base(task)
        {
            expectedVersion = version;
            this.owner = owner;
        }

        internal void Invoke()
        {
            if (owner?.version.VolatileRead() == expectedVersion)
            {
                lock (owner)
                {
                    if (owner.version == expectedVersion)
                        owner.EnqueueCompletedTask(this);
                }
            }

            owner = null;
        }
    }

    private LinkedTaskNode? firstTask, lastTask;

    private void EnqueueCompletedTask(LinkedTaskNode node)
    {
        Debug.Assert(Monitor.IsEntered(this));
        Debug.Assert(node is { Task: { IsCompleted: true } });

        if (firstTask is null || lastTask is null)
        {
            firstTask = lastTask = node;
        }
        else
        {
            lastTask = lastTask.Next = node;
        }

        DrainWaitQueue(value: true);
    }

    private bool TryDequeueCompletedTask([NotNullWhen(true)] out T? task)
    {
        if (firstTask is not null)
        {
            task = firstTask.Task;
            var next = firstTask.Next;
            firstTask.Next = null; // help GC
            if ((firstTask = next) is null)
                lastTask = null;

            return true;
        }

        task = null;
        return false;
    }

    private void ClearTaskQueue() => firstTask = lastTask = null;
}