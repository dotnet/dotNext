using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices;

using Threading.Tasks;

/// <summary>
/// Represents an awaitable object that switches the asynchronous method to the specified task scheduler,
/// and returns the scope that switches the method back.
/// </summary>
/// <seealso cref="TaskSchedulerScope"/>
[StructLayout(LayoutKind.Auto)]
public readonly struct ScopedTaskSchedulerAwaitable
{
    private readonly TaskScheduler scheduler;

    internal ScopedTaskSchedulerAwaitable(TaskScheduler scheduler) => this.scheduler = scheduler;

    /// <summary>
    /// Gets the awaiter of this object.
    /// </summary>
    /// <remarks>
    /// The awaiter captures the synchronization context or the scheduler of the caller
    /// to be restored by <see cref="TaskSchedulerScope.DisposeAsync"/>.
    /// </remarks>
    /// <returns>The awaiter of this object.</returns>
    public Awaiter GetAwaiter() => new(ContinuationHelpers.CaptureSchedulingContext(), scheduler);

    /// <summary>
    /// Represents the awaiter that switches the asynchronous method to the specified task scheduler.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Awaiter : ICriticalNotifyCompletion
    {
        private readonly object? previous;
        private readonly TaskSchedulerAwaitable.Awaiter awaiter;

        internal Awaiter(object? previous, TaskScheduler scheduler)
        {
            this.previous = previous;
            awaiter = new(scheduler);
        }

        /// <summary>
        /// Gets a value indicating that the caller is already running on the target scheduler,
        /// so the switch is not needed.
        /// </summary>
        public bool IsCompleted => awaiter.IsCompleted;

        /// <summary>
        /// Ends the switch.
        /// </summary>
        /// <returns>The scope that switches the asynchronous method back to the original scheduler.</returns>
        public TaskSchedulerScope GetResult() => new(previous);

        /// <inheritdoc/>
        public void OnCompleted(Action continuation) => awaiter.OnCompleted(continuation);

        /// <inheritdoc/>
        public void UnsafeOnCompleted(Action continuation) => awaiter.UnsafeOnCompleted(continuation);
    }
}
