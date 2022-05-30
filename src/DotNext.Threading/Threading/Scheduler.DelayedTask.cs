using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading;

/// <summary>
/// Represents timer-based scheduler.
/// </summary>
public static partial class Scheduler
{
    /// <summary>
    /// Represents a task with delayed completion.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct DelayedTask
    {
        private readonly CancellationTokenSource? cts;
        private readonly Task? task;

        internal DelayedTask(Task task, CancellationTokenSource cts)
        {
            this.cts = cts;
            this.task = task;
        }

        internal DelayedTask(CancellationToken token)
        {
            Debug.Assert(token.IsCancellationRequested);

            task = Task.FromCanceled(token);
            cts = null;
        }

        /// <summary>
        /// Gets the underlying task.
        /// </summary>
        public Task Task => task ?? Task.FromCanceled(new(true));

        /// <summary>
        /// Cancels scheduled task.
        /// </summary>
        public void Cancel()
        {
            try
            {
                cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // suppress exception without any action
            }
        }

        /// <summary>
        /// Gets the underlying task.
        /// </summary>
        /// <param name="task">The delayed task.</param>
        /// <returns>The underlying task.</returns>
        public static implicit operator Task(in DelayedTask task) => task.Task;
    }

    /// <summary>
    /// Represents a task with delayed completion.
    /// </summary>
    /// <typeparam name="TResult">The type of the result produced by this task.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct DelayedTask<TResult>
    {
        private readonly CancellationTokenSource? cts;
        private readonly Task<TResult>? task;

        internal DelayedTask(Task<TResult> task, CancellationTokenSource cts)
        {
            this.cts = cts;
            this.task = task;
        }

        internal DelayedTask(CancellationToken token)
        {
            Debug.Assert(token.IsCancellationRequested);

            task = System.Threading.Tasks.Task.FromCanceled<TResult>(token);
            cts = null;
        }

        /// <summary>
        /// Gets the underlying task.
        /// </summary>
        public Task<TResult> Task => task ?? System.Threading.Tasks.Task.FromCanceled<TResult>(new(true));

        /// <summary>
        /// Cancels scheduled task.
        /// </summary>
        public void Cancel()
        {
            try
            {
                cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // suppress exception without any action
            }
        }

        /// <summary>
        /// Gets the underlying task.
        /// </summary>
        /// <param name="task">The delayed task.</param>
        /// <returns>The underlying task.</returns>
        public static implicit operator Task<TResult>(in DelayedTask<TResult> task) => task.Task;
    }
}