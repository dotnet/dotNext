using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading.Tasks;

/// <summary>
/// Represents task synchronization methods.
/// </summary>
public static partial class Synchronization
{
    /// <summary>
    /// Gets task result synchronously.
    /// </summary>
    /// <param name="task">The task to synchronize.</param>
    /// <param name="timeout">Synchronization timeout.</param>
    /// <typeparam name="TResult">Type of task result.</typeparam>
    /// <returns>Task result.</returns>
    /// <exception cref="TimeoutException">Task is not completed.</exception>
    public static Result<TResult> GetResult<TResult>(this Task<TResult> task, TimeSpan timeout)
    {
        Result<TResult> result;
        try
        {
            result = task.Wait(timeout) ? new(task.Result) : new(new TimeoutException());
        }
        catch (AggregateException e) when (e.InnerExceptions.Count == 1)
        {
            result = new(e.InnerExceptions[0]);
        }
        catch (Exception e)
        {
            result = new(e);
        }

        return result;
    }

    /// <summary>
    /// Gets task result synchronously.
    /// </summary>
    /// <param name="task">The task to synchronize.</param>
    /// <param name="token">Cancellation token.</param>
    /// <typeparam name="TResult">Type of task result.</typeparam>
    /// <returns>Task result.</returns>
    public static Result<TResult> GetResult<TResult>(this Task<TResult> task, CancellationToken token)
    {
        Result<TResult> result;
        try
        {
            task.Wait(token);
            result = task.Result;
        }
        catch (AggregateException e) when (e.InnerExceptions.Count == 1)
        {
            result = new(e.InnerExceptions[0]);
        }
        catch (Exception e)
        {
            result = new(e);
        }

        return result;
    }

    /// <summary>
    /// Attempts to get the result of the task if it is completed.
    /// </summary>
    /// <typeparam name="TResult">Type of task result.</typeparam>
    /// <param name="task">The task to synchronize.</param>
    /// <returns>Task result; or <see langword="null"/> if <paramref name="task"/> is not completed.</returns>
    public static Result<TResult>? TryGetResult<TResult>(this Task<TResult>? task)
        => task is { IsCompleted: true } ? GetResult(task, CancellationToken.None) : null;

    /// <summary>
    /// Gets task result synchronously.
    /// </summary>
    /// <param name="task">The task to synchronize.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>Task result; or <see cref="System.Reflection.Missing.Value"/> returned from <see cref="Result{T}.Value"/> if <paramref name="task"/> is not of type <see cref="Task{TResult}"/>.</returns>
    [RequiresUnreferencedCode("Runtime binding may be incompatible with IL trimming")]
    public static Result<dynamic?> GetResult(this Task task, CancellationToken token)
    {
        Result<object?> result;
        try
        {
            task.Wait(token);
            var awaiter = new DynamicTaskAwaitable.Awaiter(task, false);
            result = new(awaiter.GetRawResult());
        }
        catch (AggregateException e) when (e.InnerExceptions.Count == 1)
        {
            result = new(e.InnerExceptions[0]);
        }
        catch (Exception e)
        {
            result = new(e);
        }

        return result;
    }

    /// <summary>
    /// Gets task result synchronously.
    /// </summary>
    /// <param name="task">The task to synchronize.</param>
    /// <param name="timeout">Synchronization timeout.</param>
    /// <returns>Task result; or <see cref="System.Reflection.Missing.Value"/> returned from <see cref="Result{T}.Value"/> if <paramref name="task"/> is not of type <see cref="Task{TResult}"/>.</returns>
    /// <exception cref="TimeoutException">Task is not completed.</exception>
    [RequiresUnreferencedCode("Runtime binding may be incompatible with IL trimming")]
    public static Result<dynamic?> GetResult(this Task task, TimeSpan timeout)
    {
        Result<dynamic?> result;
        try
        {
            if (!task.Wait(timeout))
                throw new TimeoutException();

            var awaiter = new DynamicTaskAwaitable.Awaiter(task, false);
            result = new(awaiter.GetRawResult());
        }
        catch (AggregateException e) when (e.InnerExceptions.Count == 1)
        {
            result = new(e.InnerExceptions[0]);
        }
        catch (Exception e)
        {
            result = new(e);
        }

        return result;
    }

    /// <summary>
    /// Creates a task that will complete when all of the passed tasks have completed.
    /// </summary>
    /// <typeparam name="T1">The type of the first task.</typeparam>
    /// <typeparam name="T2">The type of the second task.</typeparam>
    /// <param name="task1">The first task to await.</param>
    /// <param name="task2">The second task to await.</param>
    /// <returns>The task containing results of both tasks.</returns>
    public static async Task<(Result<T1>, Result<T2>)> WhenAll<T1, T2>(Task<T1> task1, Task<T2> task2)
    {
        (Result<T1>, Result<T2>) result;

        try
        {
            result.Item1 = await task1.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            result.Item1 = new(e);
        }

        try
        {
            result.Item2 = await task2.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            result.Item2 = new(e);
        }

        return result;
    }

    /// <summary>
    /// Creates a task that will complete when all of the passed tasks have completed.
    /// </summary>
    /// <typeparam name="T1">The type of the first task.</typeparam>
    /// <typeparam name="T2">The type of the second task.</typeparam>
    /// <typeparam name="T3">The type of the third task.</typeparam>
    /// <param name="task1">The first task to await.</param>
    /// <param name="task2">The second task to await.</param>
    /// <param name="task3">The third task to await.</param>
    /// <returns>The task containing results of all tasks.</returns>
    public static async Task<(Result<T1>, Result<T2>, Result<T3>)> WhenAll<T1, T2, T3>(Task<T1> task1, Task<T2> task2, Task<T3> task3)
    {
        (Result<T1>, Result<T2>, Result<T3>) result;

        try
        {
            result.Item1 = await task1.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            result.Item1 = new(e);
        }

        try
        {
            result.Item2 = await task2.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            result.Item2 = new(e);
        }

        try
        {
            result.Item3 = await task3.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            result.Item3 = new(e);
        }

        return result;
    }

    /// <summary>
    /// Creates a task that will complete when all of the passed tasks have completed.
    /// </summary>
    /// <typeparam name="T1">The type of the first task.</typeparam>
    /// <typeparam name="T2">The type of the second task.</typeparam>
    /// <typeparam name="T3">The type of the third task.</typeparam>
    /// <typeparam name="T4">The type of the fourth task.</typeparam>
    /// <param name="task1">The first task to await.</param>
    /// <param name="task2">The second task to await.</param>
    /// <param name="task3">The third task to await.</param>
    /// <param name="task4">The fourth task to await.</param>
    /// <returns>The task containing results of all tasks.</returns>
    public static async Task<(Result<T1>, Result<T2>, Result<T3>, Result<T4>)> WhenAll<T1, T2, T3, T4>(Task<T1> task1, Task<T2> task2, Task<T3> task3, Task<T4> task4)
    {
        (Result<T1>, Result<T2>, Result<T3>, Result<T4>) result;

        try
        {
            result.Item1 = await task1.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            result.Item1 = new(e);
        }

        try
        {
            result.Item2 = await task2.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            result.Item2 = new(e);
        }

        try
        {
            result.Item3 = await task3.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            result.Item3 = new(e);
        }

        try
        {
            result.Item4 = await task4.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            result.Item4 = new(e);
        }

        return result;
    }

    /// <summary>
    /// Creates a task that will complete when all of the passed tasks have completed.
    /// </summary>
    /// <typeparam name="T1">The type of the first task.</typeparam>
    /// <typeparam name="T2">The type of the second task.</typeparam>
    /// <typeparam name="T3">The type of the third task.</typeparam>
    /// <typeparam name="T4">The type of the fourth task.</typeparam>
    /// <typeparam name="T5">The type of the fifth task.</typeparam>
    /// <param name="task1">The first task to await.</param>
    /// <param name="task2">The second task to await.</param>
    /// <param name="task3">The third task to await.</param>
    /// <param name="task4">The fourth task to await.</param>
    /// <param name="task5">The fifth task to await.</param>
    /// <returns>The task containing results of all tasks.</returns>
    public static async Task<(Result<T1>, Result<T2>, Result<T3>, Result<T4>, Result<T5>)> WhenAll<T1, T2, T3, T4, T5>(Task<T1> task1, Task<T2> task2, Task<T3> task3, Task<T4> task4, Task<T5> task5)
    {
        (Result<T1>, Result<T2>, Result<T3>, Result<T4>, Result<T5>) result;

        try
        {
            result.Item1 = await task1.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            result.Item1 = new(e);
        }

        try
        {
            result.Item2 = await task2.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            result.Item2 = new(e);
        }

        try
        {
            result.Item3 = await task3.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            result.Item3 = new(e);
        }

        try
        {
            result.Item4 = await task4.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            result.Item4 = new(e);
        }

        try
        {
            result.Item5 = await task5.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            result.Item5 = new(e);
        }

        return result;
    }
}