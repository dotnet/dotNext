using System.Runtime.InteropServices;

namespace DotNext.Threading.Tasks;

using Runtime.CompilerServices;

/// <summary>
/// Represents the scope of the asynchronous method executed by the task scheduler.
/// </summary>
/// <remarks>
/// This type intentionally doesn't implement <see cref="IAsyncDisposable"/>, because the continuation
/// of <see cref="ValueTask"/> returned by <see cref="IAsyncDisposable.DisposeAsync"/> is executed by the scheduler
/// that is being left. The scope must be used with <see langword="await"/> <see langword="using"/> statement only.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly struct TaskSchedulerScope
{
    // SynchronizationContext, TaskScheduler or null for TaskScheduler.Default
    private readonly object? previous;

    internal TaskSchedulerScope(object? previous) => this.previous = previous;

    /// <summary>
    /// Switches the asynchronous method back to the original synchronization context or scheduler.
    /// </summary>
    /// <returns>The awaitable object that resumes the caller on the original synchronization context or scheduler.</returns>
    public SchedulingContextAwaitable DisposeAsync() => new(previous);
}