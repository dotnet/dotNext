using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks;

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
    public static Consumer<T> GetConsumer<T>(this TaskCompletionPipe<Task<T>> pipe)
        => new(pipe);

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
            => GetAsyncEnumerator<T>(pipe, pipe.Version, token);
    }
}