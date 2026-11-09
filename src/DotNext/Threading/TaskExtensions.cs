using System.Runtime.CompilerServices;

namespace DotNext.Threading;

/// <summary>
/// Extends <see cref="Task"/> type.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Extends <see cref="Task"/> with spawning methods.
    /// </summary>
    extension(Task)
    {
        /// <summary>
        /// Yields control to the caller and resumes the asynchronous method on <see cref="ThreadPool"/>.
        /// </summary>
        /// <remarks>
        /// In contrast to <see cref="Task.Yield"/>, the continuation ignores the current <see cref="SynchronizationContext"/>
        /// and <see cref="TaskScheduler"/>. For instance, awaiting this method inside a task
        /// scheduled by <see cref="ConcurrentExclusiveSchedulerPair.ExclusiveScheduler"/> leaves the exclusive scheduler.
        /// The method always yields, even if the caller is already running on the thread pool.
        /// </remarks>
        /// <returns>The awaitable object that resumes the caller on the thread pool.</returns>
        public static ConfiguredTaskAwaitable YieldToThreadPool() => Task
            .CompletedTask
            .ConfigureAwait(ConfigureAwaitOptions.ForceYielding);

        /// <summary>
        /// Queues the specified work to run on the thread pool and returns a proxy for the task returned by function.
        /// </summary>
        /// <param name="func">The work to execute asynchronously.</param>
        /// <param name="arg">The argument to be passed to the function.</param>
        /// <typeparam name="T">The type of the argument to be passed to the function.</typeparam>
        /// <returns>A task that represents a proxy for the task returned by function.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public static Task Run<T>(Func<T, Task> func, T arg)
        {
            ArgumentNullException.ThrowIfNull(func);
            
            return Run<FuncWrapper<T, Task>>(new(func, arg));
        }

        /// <summary>
        /// Queues the specified work to run on the thread pool and returns a proxy for the task returned by function.
        /// </summary>
        /// <param name="func">The work to execute asynchronously.</param>
        /// <param name="arg1">The first argument to be passed to the function.</param>
        /// <param name="arg2">The second argument to be passed to the function.</param>
        /// <typeparam name="T1">The type of the first argument to be passed to the function.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed to the function.</typeparam>
        /// <returns>A task that represents a proxy for the task returned by function.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public static Task Run<T1, T2>(Func<T1, T2, Task> func, T1 arg1, T2 arg2)
        {
            ArgumentNullException.ThrowIfNull(func);
            
            return Run<FuncWrapper<T1, T2, Task>>(new(func, arg1, arg2));
        }

        /// <summary>
        /// Queues the specified work to run on the thread pool and returns a proxy for the task returned by function.
        /// </summary>
        /// <param name="func">The work to execute asynchronously.</param>
        /// <param name="arg1">The first argument to be passed to the function.</param>
        /// <param name="arg2">The second argument to be passed to the function.</param>
        /// <param name="arg3">The third argument to be passed to the function.</param>
        /// <typeparam name="T1">The type of the first argument to be passed to the function.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed to the function.</typeparam>
        /// <typeparam name="T3">The type of the third argument to be passed to the function.</typeparam>
        /// <returns>A task that represents a proxy for the task returned by function.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public static Task Run<T1, T2, T3>(Func<T1, T2, T3, Task> func, T1 arg1, T2 arg2, T3 arg3)
        {
            ArgumentNullException.ThrowIfNull(func);
            
            return Run<FuncWrapper<T1, T2, T3, Task>>(new(func, arg1, arg2, arg3));
        }

        /// <summary>
        /// Queues the specified work to run on the thread pool and returns a proxy for the task returned by function.
        /// </summary>
        /// <param name="func">The work to execute asynchronously.</param>
        /// <param name="arg1">The first argument to be passed to the function.</param>
        /// <param name="arg2">The second argument to be passed to the function.</param>
        /// <param name="arg3">The third argument to be passed to the function.</param>
        /// <param name="arg4">The fourth argument to be passed to the function.</param>
        /// <typeparam name="T1">The type of the first argument to be passed to the function.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed to the function.</typeparam>
        /// <typeparam name="T3">The type of the third argument to be passed to the function.</typeparam>
        /// <typeparam name="T4">The type of the fourth argument to be passed to the function.</typeparam>
        /// <returns>A task that represents a proxy for the task returned by function.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public static Task Run<T1, T2, T3, T4>(Func<T1, T2, T3, T4, Task> func, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            ArgumentNullException.ThrowIfNull(func);
            
            return Run<FuncWrapper<T1, T2, T3, T4, Task>>(new(func, arg1, arg2, arg3, arg4));
        }

        /// <summary>
        /// Queues the specified work to run on the thread pool and returns a proxy for the task returned by function.
        /// </summary>
        /// <param name="workItem">The work to execute asynchronously.</param>
        /// <typeparam name="TWorkItem">The type of the work to execute asynchronously.</typeparam>
        /// <returns>A task that represents a proxy for the task returned by function.</returns>
        public static async Task Run<TWorkItem>(TWorkItem workItem)
            where TWorkItem : ISupplier<Task>
        {
            await Task.YieldToThreadPool();
            await workItem.Invoke().ConfigureAwait(false);
        }

        /// <summary>
        /// Queues the specified work to run on the thread pool and returns a proxy for the task returned by function.
        /// </summary>
        /// <param name="func">The work to execute asynchronously.</param>
        /// <param name="arg">The argument to be passed to the function.</param>
        /// <typeparam name="T">The type of the argument to be passed to the function.</typeparam>
        /// <typeparam name="TResult">The result of the function.</typeparam>
        /// <returns>A task that represents a proxy for the task returned by function.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public static Task<TResult> Run<T, TResult>(Func<T, Task<TResult>> func, T arg)
        {
            ArgumentNullException.ThrowIfNull(func);
            
            return Run<TResult, FuncWrapper<T, Task<TResult>>>(new(func, arg));
        }

        /// <summary>
        /// Queues the specified work to run on the thread pool and returns a proxy for the task returned by function.
        /// </summary>
        /// <param name="func">The work to execute asynchronously.</param>
        /// <param name="arg1">The first argument to be passed to the function.</param>
        /// <param name="arg2">The second argument to be passed to the function.</param>
        /// <typeparam name="T1">The type of the first argument to be passed to the function.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed to the function.</typeparam>
        /// <typeparam name="TResult">The result of the function.</typeparam>
        /// <returns>A task that represents a proxy for the task returned by function.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public static Task<TResult> Run<T1, T2, TResult>(Func<T1, T2, Task<TResult>> func, T1 arg1, T2 arg2)
        {
            ArgumentNullException.ThrowIfNull(func);
            
            return Run<TResult, FuncWrapper<T1, T2, Task<TResult>>>(new(func, arg1, arg2));
        }

        /// <summary>
        /// Queues the specified work to run on the thread pool and returns a proxy for the task returned by function.
        /// </summary>
        /// <param name="func">The work to execute asynchronously.</param>
        /// <param name="arg1">The first argument to be passed to the function.</param>
        /// <param name="arg2">The second argument to be passed to the function.</param>
        /// <param name="arg3">The third argument to be passed to the function.</param>
        /// <typeparam name="T1">The type of the first argument to be passed to the function.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed to the function.</typeparam>
        /// <typeparam name="T3">The type of the third argument to be passed to the function.</typeparam>
        /// <typeparam name="TResult">The result of the function.</typeparam>
        /// <returns>A task that represents a proxy for the task returned by function.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public static Task<TResult> Run<T1, T2, T3, TResult>(Func<T1, T2, T3, Task<TResult>> func, T1 arg1, T2 arg2, T3 arg3)
        {
            ArgumentNullException.ThrowIfNull(func);
            
            return Run<TResult, FuncWrapper<T1, T2, T3, Task<TResult>>>(new(func, arg1, arg2, arg3));
        }

        /// <summary>
        /// Queues the specified work to run on the thread pool and returns a proxy for the task returned by function.
        /// </summary>
        /// <param name="func">The work to execute asynchronously.</param>
        /// <param name="arg1">The first argument to be passed to the function.</param>
        /// <param name="arg2">The second argument to be passed to the function.</param>
        /// <param name="arg3">The third argument to be passed to the function.</param>
        /// <param name="arg4">The fourth argument to be passed to the function.</param>
        /// <typeparam name="T1">The type of the first argument to be passed to the function.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed to the function.</typeparam>
        /// <typeparam name="T3">The type of the third argument to be passed to the function.</typeparam>
        /// <typeparam name="T4">The type of the fourth argument to be passed to the function.</typeparam>
        /// <typeparam name="TResult">The result of the function.</typeparam>
        /// <returns>A task that represents a proxy for the task returned by function.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public static Task<TResult> Run<T1, T2, T3, T4, TResult>(Func<T1, T2, T3, T4, Task<TResult>> func, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            ArgumentNullException.ThrowIfNull(func);
            
            return Run<TResult, FuncWrapper<T1, T2, T3, T4, Task<TResult>>>(new(func, arg1, arg2, arg3, arg4));
        }

        /// <summary>
        /// Queues the specified work to run on the thread pool and returns a proxy for the task returned by function.
        /// </summary>
        /// <param name="workItem">The work to execute asynchronously.</param>
        /// <typeparam name="TResult">The result of the function.</typeparam>
        /// <typeparam name="TWorkItem">The type of the work to execute asynchronously.</typeparam>
        /// <returns>A task that represents a proxy for the task returned by function.</returns>
        public static async Task<TResult> Run<TResult, TWorkItem>(TWorkItem workItem)
            where TWorkItem : ISupplier<Task<TResult>>
        {
            await Task.YieldToThreadPool();
            return await workItem.Invoke().ConfigureAwait(false);
        }
    }
}