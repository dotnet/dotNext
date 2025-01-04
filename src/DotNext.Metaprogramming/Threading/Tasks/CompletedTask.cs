using System.Runtime.InteropServices;

namespace DotNext.Threading.Tasks;

/// <summary>
/// Represents <see cref="Task"/> or <see cref="ValueTask"/>
/// completed synchronously.
/// </summary>
/// <param name="failure">The exception with which to complete the task.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly struct CompletedTask(Exception failure)
{
    private readonly Exception? failure = failure;

    /// <summary>
    /// Obtains <see cref="Task"/> completed synchronously.
    /// </summary>
    /// <param name="task">Completed task.</param>
    public static implicit operator Task(CompletedTask task)
        => task.failure is { } f ? Task.FromException(f) : Task.CompletedTask;

    /// <summary>
    /// Obtains <see cref="ValueTask"/> completed synchronously.
    /// </summary>
    /// <param name="task">Completed task.</param>
    public static implicit operator ValueTask(CompletedTask task)
        => task.failure is { } f ? ValueTask.FromException(f) : ValueTask.CompletedTask;
}

/// <summary>
/// Represents <see cref="Task{TResult}"/> or <see cref="ValueTask{TResult}"/>
/// completed synchronously.
/// </summary>
/// <typeparam name="T">The type of the result produced by the task.</typeparam>
[StructLayout(LayoutKind.Auto)]
internal readonly struct CompletedTask<T>
{
    private readonly Result<T> result;

    /// <summary>
    /// Creates task that has completed with a specified exception.
    /// </summary>
    /// <param name="failure">The exception with which to complete the task.</param>
    public CompletedTask(Exception failure) => result = new(failure);

    /// <summary>
    /// Creates task that has completed successfully with a specified result.
    /// </summary>
    /// <param name="result">The task result.</param>
    public CompletedTask(T result) => this.result = new(result);

    /// <summary>
    /// Obtains <see cref="Task{TResult}"/> completed synchronously.
    /// </summary>
    /// <param name="task">Completed task.</param>
    public static implicit operator Task<T>(CompletedTask<T> task)
        => task.result.AsTask().AsTask();

    /// <summary>
    /// Obtains <see cref="ValueTask{TResult}"/> completed synchronously.
    /// </summary>
    /// <param name="task">Completed task.</param>
    public static implicit operator ValueTask<T>(CompletedTask<T> task)
        => task.result.AsTask();
}