using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Multiplexing;

partial class MultiplexedListener
{
    private sealed class TaskNode
    {
        private readonly WeakReference<Task> task;
        private TaskNode? next;

        private TaskNode(Task task)
            => this.task = new(task);

        private TaskNode? CleanupAndGetNext()
        {
            var result = next;
            next = null;
            return result;
        }

        private Task AttachedTask
        {
            get
            {
                if (!task.TryGetTarget(out var result))
                    result = Task.CompletedTask;

                return result;
            }
        }

        private bool IsCompleted => AttachedTask.IsCompleted;

        public static void Add([NotNull] ref TaskNode? head, Task task)
        {
            RemoveCompleted(ref head);
            head = new(task) { next = head };
        }

        private static void RemoveCompleted(ref TaskNode? head)
        {
            for (TaskNode? current = head, previous = null; current is not null;)
            {
                if (current.IsCompleted)
                {
                    ref var next = ref previous is not null ? ref previous.next : ref head;
                    next = current = current.CleanupAndGetNext(); // previous remains unchanged
                }
                else
                {
                    previous = current;
                    current = current.next;
                }
            }
        }

        public static IEnumerable<Task> GetTasks(TaskNode? head)
        {
            return head is null ? [] : GetTasksImpl(head);

            static IEnumerable<Task> GetTasksImpl(TaskNode? node)
            {
                while (node is not null)
                {
                    if (node is { AttachedTask: { IsCompleted: false } task })
                        yield return task;

                    node = node.next;
                }
            }
        }
    }
}