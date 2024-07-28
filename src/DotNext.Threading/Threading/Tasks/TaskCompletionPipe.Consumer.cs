using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks;

using static Collections.Generic.AsyncEnumerable;

/// <summary>
/// Provides various extension methods for <see cref="TaskCompletionPipe{T}"/> class.
/// </summary>
public static class TaskCompletionPipe
{
    /// <summary>
    /// Gets asynchronous consumer.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the consuming collection.</typeparam>
    /// <param name="pipe">The task completion pipe with typed tasks.</param>
    /// <returns>The asynchronous consuming collection.</returns>
    public static Consumer<T> Consume<T>(this TaskCompletionPipe<Task<T>> pipe)
        => new(pipe);

    /// <summary>
    /// Gets a collection over tasks to be available as they complete.
    /// </summary>
    /// <param name="tasks">A collection of tasks.</param>
    /// <typeparam name="T">The result type of tasks.</typeparam>
    /// <returns>A collection over task results to be available as they complete.</returns>
    public static Consumer<T> Consume<T>(this ReadOnlySpan<Task<T>> tasks)
    {
        Consumer<T> result;
        if (tasks.IsEmpty)
        {
            result = default;
        }
        else
        {
            var pipe = new TaskCompletionPipe<Task<T>>();
            pipe.Add(tasks, complete: true);
            result = new(pipe);
        }

        return result;
    }

    /// <summary>
    /// Creates a collection over tasks to be available as they complete.
    /// </summary>
    /// <param name="tasks">A collection of tasks.</param>
    /// <typeparam name="T">The type of tasks.</typeparam>
    /// <returns>A collection over tasks to be available as they complete.</returns>
    public static IAsyncEnumerable<T> WhenEach<T>(ReadOnlySpan<T> tasks)
        where T : Task
    {
        IAsyncEnumerable<T> result;

        if (tasks.IsEmpty)
        {
            result = Empty<T>();
        }
        else
        {
            var pipe = new TaskCompletionPipe<T>();
            pipe.Add(tasks, complete: true);
            result = pipe;
        }

        return result;
    }

    private static async IAsyncEnumerator<T> GetAsyncEnumerator<T>(TaskCompletionPipe<Task<T>> pipe, uint expectedVersion, CancellationToken token)
    {
        while (await pipe.TryDequeue(expectedVersion, out var task, token).ConfigureAwait(false))
        {
            if (task is not null)
            {
                Debug.Assert(task.IsCompleted);

                yield return await task.ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Represents asynchronous consumer for the pipe.
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the pipe.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Consumer<T> : IAsyncEnumerable<T>
    {
        private readonly TaskCompletionPipe<Task<T>> pipe;

        internal Consumer(TaskCompletionPipe<Task<T>> pipe)
            => this.pipe = pipe;

        /// <summary>
        /// Gets asynchronous enumerator over completed tasks.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The asynchronous enumerator over completed tasks.</returns>
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token = default)
            => pipe is null ? Empty<T>().GetAsyncEnumerator(token) : GetAsyncEnumerator<T>(pipe, pipe.Version, token);
    }
}