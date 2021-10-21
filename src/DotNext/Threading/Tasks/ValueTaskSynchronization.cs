using System.Runtime.CompilerServices;

namespace DotNext.Threading.Tasks;

using ExceptionAggregator = Runtime.ExceptionServices.ExceptionAggregator;

/// <summary>
/// Provides a set of methods for synchronization and combination of multiple <see cref="ValueTask"/>s.
/// </summary>
/// <remarks>
/// Methods in this class exist for architectural symmetry with <c>WhenAll</c> and <c>WhenAny</c> methods
/// from <see cref="Task"/> class when you have to work with tasks implemented as value types.
/// Don't use these methods just to avoid allocation of memory inside of managed heap.
/// </remarks>
public static class ValueTaskSynchronization
{
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private static async ValueTask WhenAll<T>(T tasks)
        where T : struct, ITuple
    {
        var aggregator = new ExceptionAggregator();

        for (var i = 0; i < tasks.Length; i++)
        {
            try
            {
                await GetTask(tasks, i).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                aggregator.Add(e);
            }
        }

        aggregator.ThrowIfNeeded();

        static ValueTask GetTask(in T tuple, int index)
            => Unsafe.Add(ref Unsafe.As<T, ValueTask>(ref Unsafe.AsRef(in tuple)), index);
    }

    /// <summary>
    /// Creates a task that will complete when all of the passed tasks have completed.
    /// </summary>
    /// <remarks>
    /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
    /// </remarks>
    /// <param name="task1">The first task to await.</param>
    /// <param name="task2">The second task to await.</param>
    /// <returns>A task that represents the completion of all of the supplied tasks.</returns>
    public static ValueTask WhenAll(ValueTask task1, ValueTask task2)
        => WhenAll((task1, task2));

    /// <summary>
    /// Creates a task that will complete when all of the passed tasks have completed.
    /// </summary>
    /// <remarks>
    /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
    /// </remarks>
    /// <typeparam name="T1">The type of the first task.</typeparam>
    /// <typeparam name="T2">The type of the second task.</typeparam>
    /// <param name="task1">The first task to await.</param>
    /// <param name="task2">The second task to await.</param>
    /// <returns>A task containing results of both tasks.</returns>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<(Result<T1>, Result<T2>)> WhenAll<T1, T2>(ValueTask<T1> task1, ValueTask<T2> task2)
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
    /// <remarks>
    /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
    /// </remarks>
    /// <param name="task1">The first task to await.</param>
    /// <param name="task2">The second task to await.</param>
    /// <param name="task3">The third task to await.</param>
    /// <returns>A task that represents the completion of all of the supplied tasks.</returns>
    public static ValueTask WhenAll(ValueTask task1, ValueTask task2, ValueTask task3)
        => WhenAll((task1, task2, task3));

    /// <summary>
    /// Creates a task that will complete when all of the passed tasks have completed.
    /// </summary>
    /// <remarks>
    /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
    /// </remarks>
    /// <typeparam name="T1">The type of the first task.</typeparam>
    /// <typeparam name="T2">The type of the second task.</typeparam>
    /// <typeparam name="T3">The type of the third task.</typeparam>
    /// <param name="task1">The first task to await.</param>
    /// <param name="task2">The second task to await.</param>
    /// <param name="task3">The third task to await.</param>
    /// <returns>A task containing results of all tasks.</returns>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<(Result<T1>, Result<T2>, Result<T3>)> WhenAll<T1, T2, T3>(ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3)
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
    /// <remarks>
    /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
    /// </remarks>
    /// <param name="task1">The first task to await.</param>
    /// <param name="task2">The second task to await.</param>
    /// <param name="task3">The third task to await.</param>
    /// <param name="task4">The fourth task to await.</param>
    /// <returns>A task that represents the completion of all of the supplied tasks.</returns>
    public static ValueTask WhenAll(ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4)
        => WhenAll((task1, task2, task3, task4));

    /// <summary>
    /// Creates a task that will complete when all of the passed tasks have completed.
    /// </summary>
    /// <remarks>
    /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
    /// </remarks>
    /// <typeparam name="T1">The type of the first task.</typeparam>
    /// <typeparam name="T2">The type of the second task.</typeparam>
    /// <typeparam name="T3">The type of the third task.</typeparam>
    /// <typeparam name="T4">The type of the fourth task.</typeparam>
    /// <param name="task1">The first task to await.</param>
    /// <param name="task2">The second task to await.</param>
    /// <param name="task3">The third task to await.</param>
    /// <param name="task4">The fourth task to await.</param>
    /// <returns>A task containing results of all tasks.</returns>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<(Result<T1>, Result<T2>, Result<T3>, Result<T4>)> WhenAll<T1, T2, T3, T4>(ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4)
    {
        (Result<T1>, Result<T2>, Result<T3>, Result<T4>) result = default;

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
    /// <remarks>
    /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
    /// </remarks>
    /// <param name="task1">The first task to await.</param>
    /// <param name="task2">The second task to await.</param>
    /// <param name="task3">The third task to await.</param>
    /// <param name="task4">The fourth task to await.</param>
    /// <param name="task5">The fifth task to await.</param>
    /// <returns>A task that represents the completion of all of the supplied tasks.</returns>
    public static ValueTask WhenAll(ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5)
        => WhenAll((task1, task2, task3, task4, task5));

    /// <summary>
    /// Creates a task that will complete when all of the passed tasks have completed.
    /// </summary>
    /// <remarks>
    /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
    /// </remarks>
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
    /// <returns>A task containing results of all tasks.</returns>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<(Result<T1>, Result<T2>, Result<T3>, Result<T4>, Result<T5>)> WhenAll<T1, T2, T3, T4, T5>(ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5)
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