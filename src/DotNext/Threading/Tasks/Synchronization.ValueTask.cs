using System.Runtime.CompilerServices;
using static System.Threading.Timeout;

namespace DotNext.Threading.Tasks;

using ExceptionAggregator = Runtime.ExceptionServices.ExceptionAggregator;

public static partial class Synchronization
{
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
    /// Creates a task that will complete when all the passed tasks have completed.
    /// </summary>
    /// <remarks>
    /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
    /// </remarks>
    /// <param name="task1">The first task to await.</param>
    /// <param name="task2">The second task to await.</param>
    /// <returns>A task that represents the completion of all the supplied tasks.</returns>
    public static ValueTask WhenAll(ValueTask task1, ValueTask task2)
        => WhenAll((task1, task2));

    /// <summary>
    /// Creates a task that will complete when all the passed tasks have completed.
    /// </summary>
    /// <remarks>
    /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
    /// </remarks>
    /// <typeparam name="T1">The type of the first task.</typeparam>
    /// <typeparam name="T2">The type of the second task.</typeparam>
    /// <param name="task1">The first task to await.</param>
    /// <param name="task2">The second task to await.</param>
    /// <returns>A task containing results of both tasks.</returns>
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
    /// Creates a task that will complete when all the passed tasks have completed.
    /// </summary>
    /// <remarks>
    /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
    /// </remarks>
    /// <param name="task1">The first task to await.</param>
    /// <param name="task2">The second task to await.</param>
    /// <param name="task3">The third task to await.</param>
    /// <returns>A task that represents the completion of all the supplied tasks.</returns>
    public static ValueTask WhenAll(ValueTask task1, ValueTask task2, ValueTask task3)
        => WhenAll((task1, task2, task3));

    /// <summary>
    /// Creates a task that will complete when all the passed tasks have completed.
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
    /// Creates a task that will complete when all the passed tasks have completed.
    /// </summary>
    /// <remarks>
    /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
    /// </remarks>
    /// <param name="task1">The first task to await.</param>
    /// <param name="task2">The second task to await.</param>
    /// <param name="task3">The third task to await.</param>
    /// <param name="task4">The fourth task to await.</param>
    /// <returns>A task that represents the completion of all the supplied tasks.</returns>
    public static ValueTask WhenAll(ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4)
        => WhenAll((task1, task2, task3, task4));

    /// <summary>
    /// Creates a task that will complete when all the passed tasks have completed.
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
    /// Creates a task that will complete when all the passed tasks have completed.
    /// </summary>
    /// <remarks>
    /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
    /// </remarks>
    /// <param name="task1">The first task to await.</param>
    /// <param name="task2">The second task to await.</param>
    /// <param name="task3">The third task to await.</param>
    /// <param name="task4">The fourth task to await.</param>
    /// <param name="task5">The fifth task to await.</param>
    /// <returns>A task that represents the completion of all the supplied tasks.</returns>
    public static ValueTask WhenAll(ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5)
        => WhenAll((task1, task2, task3, task4, task5));

    /// <summary>
    /// Creates a task that will complete when all the passed tasks have completed.
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
    
    /// <summary>
    /// Waits for the task synchronously.
    /// </summary>
    /// <remarks>
    /// In contrast to <see cref="Task.Wait()"/> this method doesn't use wait handles.
    /// </remarks>
    /// <param name="task">The task to wait.</param>
    public static void Wait(this in ValueTask task)
    {
        var awaiter = task.ConfigureAwait(false).GetAwaiter();

        if (!SpinWait(in awaiter))
            BlockingWait(in awaiter);
        
        awaiter.GetResult();

        static bool SpinWait(ref readonly ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter awaiter)
        {
            bool result;
            for (var spinner = new SpinWait();; spinner.SpinOnce())
            {
                if ((result = awaiter.IsCompleted) || spinner.NextSpinWillYield)
                    break;
            }

            return result;
        }

        static void BlockingWait(ref readonly ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter awaiter)
        {
            awaiter.UnsafeOnCompleted(Thread.CurrentThread.Interrupt);
            try
            {
                // park thread
                Thread.Sleep(Infinite);
            }
            catch (ThreadInterruptedException) when (awaiter.IsCompleted)
            {
                // suppress exception
            }
        }
    }

    /// <summary>
    /// Waits for the task synchronously.
    /// </summary>
    /// <remarks>
    /// In contrast to <see cref="Task{TResult}.Wait()"/> this method doesn't use wait handles.
    /// </remarks>
    /// <typeparam name="T">The type of the task result.</typeparam>
    /// <param name="task">The task to wait.</param>
    public static T Wait<T>(this in ValueTask<T> task)
    {
        var awaiter = task.ConfigureAwait(false).GetAwaiter();

        if (!SpinWait(in awaiter))
            BlockingWait(in awaiter);
        
        return awaiter.GetResult();

        static bool SpinWait(ref readonly ConfiguredValueTaskAwaitable<T>.ConfiguredValueTaskAwaiter awaiter)
        {
            bool result;
            for (var spinner = new SpinWait();; spinner.SpinOnce())
            {
                if ((result = awaiter.IsCompleted) || spinner.NextSpinWillYield)
                    break;
            }

            return result;
        }

        static void BlockingWait(ref readonly ConfiguredValueTaskAwaitable<T>.ConfiguredValueTaskAwaiter awaiter)
        {
            awaiter.UnsafeOnCompleted(Thread.CurrentThread.Interrupt);
            try
            {
                // park thread
                Thread.Sleep(Infinite);
            }
            catch (ThreadInterruptedException) when (awaiter.IsCompleted)
            {
                // suppress exception
            }
        }
    }
}