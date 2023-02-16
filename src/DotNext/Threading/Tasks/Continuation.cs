namespace DotNext.Threading.Tasks;

using Generic;

/// <summary>
/// Represents various continuations.
/// </summary>
public static class Continuation
{
    /// <summary>
    /// Allows to obtain original <see cref="Task"/> in its final state after <c>await</c> without
    /// throwing exception produced by this task.
    /// </summary>
    /// <param name="task">The task to await.</param>
    /// <returns><paramref name="task"/> in final state.</returns>
    public static Task<Task> OnCompleted(this Task task)
        => task.ContinueWith(Func.Identity<Task>(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);

    /// <summary>
    /// Allows to obtain original <see cref="Task{R}"/> in its final state after <c>await</c> without
    /// throwing exception produced by this task.
    /// </summary>
    /// <typeparam name="TResult">The type of the task result.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <returns><paramref name="task"/> in final state.</returns>
    public static Task<Task<TResult>> OnCompleted<TResult>(this Task<TResult> task)
        => task.ContinueWith(Func.Identity<Task<TResult>>(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);

    /// <summary>
    /// Returns constant value if underlying task is failed.
    /// </summary>
    /// <remarks>
    /// This continuation doesn't produce memory pressure. The delegate representing
    /// continuation is cached for future reuse as well as constant value.
    /// </remarks>
    /// <param name="task">The task to check.</param>
    /// <param name="scheduler">Optional scheduler used to schedule continuation.</param>
    /// <typeparam name="T">The type of task result.</typeparam>
    /// <typeparam name="TConstant">The type describing constant value.</typeparam>
    /// <returns>The task representing continuation.</returns>
    public static Task<T> OnFaulted<T, TConstant>(this Task<T> task, TaskScheduler? scheduler = null)
        where TConstant : Constant<T>, new()
        => OnFaulted<T, TConstant>(task, Predicate.Constant<AggregateException>(value: true), scheduler);

    /// <summary>
    /// Returns constant value if underlying task is failed with the exception that matches
    /// to the filter.
    /// </summary>
    /// <remarks>
    /// This continuation doesn't produce memory pressure. The delegate representing
    /// continuation is cached for future reuse as well as constant value.
    /// </remarks>
    /// <param name="task">The task to check.</param>
    /// <param name="filter">The exception filter.</param>
    /// <param name="scheduler">Optional scheduler used to schedule continuation.</param>
    /// <typeparam name="T">The type of task result.</typeparam>
    /// <typeparam name="TConstant">The type describing constant value.</typeparam>
    /// <returns>The task representing continuation.</returns>
    public static Task<T> OnFaulted<T, TConstant>(this Task<T> task, Predicate<AggregateException> filter, TaskScheduler? scheduler = null)
        where TConstant : Constant<T>, new()
        => task.Status switch
        {
            TaskStatus.Faulted when filter(task.Exception!) => CompletedTask<T, TConstant>.Task,
            TaskStatus.RanToCompletion or TaskStatus.Canceled or TaskStatus.Faulted => task,
            _ => task.ContinueWith(CompletedTask<T, TConstant>.WhenFaulted, filter, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, scheduler ?? TaskScheduler.Current),
        };

    /// <summary>
    /// Returns constant value if underlying task is failed or canceled.
    /// </summary>
    /// <remarks>
    /// This continuation doesn't produce memory pressure. The delegate representing
    /// continuation is cached for future reuse as well as constant value.
    /// </remarks>
    /// <param name="task">The task to check.</param>
    /// <param name="scheduler">Optional scheduler used to schedule continuation.</param>
    /// <typeparam name="T">The type of task result.</typeparam>
    /// <typeparam name="TConstant">The type describing constant value.</typeparam>
    /// <returns>The task representing continuation.</returns>
    public static Task<T> OnFaultedOrCanceled<T, TConstant>(this Task<T> task, TaskScheduler? scheduler = null)
        where TConstant : Constant<T>, new()
        => OnFaultedOrCanceled<T, TConstant>(task, Predicate.Constant<AggregateException>(value: true), scheduler);

    /// <summary>
    /// Returns constant value if underlying task is canceled or failed with the exception that matches
    /// to the filter.
    /// </summary>
    /// <remarks>
    /// This continuation doesn't produce memory pressure. The delegate representing
    /// continuation is cached for future reuse as well as constant value.
    /// </remarks>
    /// <param name="task">The task to check.</param>
    /// <param name="filter">The exception filter.</param>
    /// <param name="scheduler">Optional scheduler used to schedule continuation.</param>
    /// <typeparam name="T">The type of task result.</typeparam>
    /// <typeparam name="TConstant">The type describing constant value.</typeparam>
    /// <returns>The task representing continuation.</returns>
    public static Task<T> OnFaultedOrCanceled<T, TConstant>(this Task<T> task, Predicate<AggregateException> filter, TaskScheduler? scheduler = null)
        where TConstant : Constant<T>, new()
    {
        switch (task.Status)
        {
            case TaskStatus.Faulted when filter(task.Exception!):
            case TaskStatus.Canceled:
                task = CompletedTask<T, TConstant>.Task;
                break;
            case TaskStatus.RanToCompletion or TaskStatus.Faulted:
                break;
            default:
                task = task.ContinueWith(CompletedTask<T, TConstant>.WhenFaultedOrCanceled, filter, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, scheduler ?? TaskScheduler.Current);
                break;
        }

        return task;
    }

    /// <summary>
    /// Returns constant value if underlying task is canceled.
    /// </summary>
    /// <remarks>
    /// This continuation doesn't produce memory pressure. The delegate representing
    /// continuation is cached for future reuse as well as constant value.
    /// </remarks>
    /// <param name="task">The task to check.</param>
    /// <param name="scheduler">Optional scheduler used to schedule continuation.</param>
    /// <typeparam name="T">The type of task result.</typeparam>
    /// <typeparam name="TConstant">The type describing constant value.</typeparam>
    /// <returns>The task representing continuation.</returns>
    public static Task<T> OnCanceled<T, TConstant>(this Task<T> task, TaskScheduler? scheduler = null)
        where TConstant : Constant<T>, new()
        => task.Status switch
        {
            TaskStatus.Canceled => CompletedTask<T, TConstant>.Task,
            TaskStatus.RanToCompletion or TaskStatus.Faulted => task,
            _ => task.ContinueWith(CompletedTask<T, TConstant>.WhenCanceled, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, scheduler ?? TaskScheduler.Current),
        };

    internal static void OnCompleted(this Task task, AsyncCallback callback)
    {
        if (task.IsCompleted)
            callback(task);
        else
            task.ConfigureAwait(false).GetAwaiter().OnCompleted(() => callback(task));
    }

    internal static Task AttachState(this Task task, object? state, CancellationToken token = default)
    {
        return task.ContinueWith(WhenFaultedOrCanceled, state, token, TaskContinuationOptions.None, TaskScheduler.Default);

        static void WhenFaultedOrCanceled(Task task, object? state)
            => task.ConfigureAwait(false).GetAwaiter().GetResult();
    }
}