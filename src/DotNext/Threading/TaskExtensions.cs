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
        internal static ConfiguredTaskAwaitable SpawningAwaitable => Task
            .CompletedTask
            .ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        
        /// <summary>
        /// Queues the specified work to run on the thread pool and returns a proxy for the task returned by function.
        /// </summary>
        /// <param name="func">The work to execute asynchronously.</param>
        /// <param name="arg">The argument to be passed to the function.</param>
        /// <typeparam name="T">The type of the argument to be passed to the function.</typeparam>
        /// <returns>A task that represents a proxy for the task returned by function.</returns>
        public static async Task Run<T>(Func<T, Task> func, T arg)
        {
            await Task.SpawningAwaitable;
            await func(arg).ConfigureAwait(false);
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
        public static async Task Run<T1, T2>(Func<T1, T2, Task> func, T1 arg1, T2 arg2)
        {
            await Task.SpawningAwaitable;
            await func(arg1, arg2).ConfigureAwait(false);
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
        public static async Task Run<T1, T2, T3>(Func<T1, T2, T3, Task> func, T1 arg1, T2 arg2, T3 arg3)
        {
            await Task.SpawningAwaitable;
            await func(arg1, arg2, arg3).ConfigureAwait(false);
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
        /// <typeparam name="T4">The type of the third argument to be passed to the function.</typeparam>
        /// <returns>A task that represents a proxy for the task returned by function.</returns>
        public static async Task Run<T1, T2, T3, T4>(Func<T1, T2, T3, T4, Task> func, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            await Task.SpawningAwaitable;
            await func(arg1, arg2, arg3, arg4).ConfigureAwait(false);
        }

        /// <summary>
        /// Queues the specified work to run on the thread pool and returns a proxy for the task returned by function.
        /// </summary>
        /// <param name="func">The work to execute asynchronously.</param>
        /// <param name="arg">The argument to be passed to the function.</param>
        /// <typeparam name="T">The type of the argument to be passed to the function.</typeparam>
        /// <typeparam name="TResult">The result of the function.</typeparam>
        /// <returns>A task that represents a proxy for the task returned by function.</returns>
        public static async Task<TResult> Run<T, TResult>(Func<T, Task<TResult>> func, T arg)
        {
            await Task.SpawningAwaitable;
            return await func(arg).ConfigureAwait(false);
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
        public static async Task<TResult> Run<T1, T2, TResult>(Func<T1, T2, Task<TResult>> func, T1 arg1, T2 arg2)
        {
            await Task.SpawningAwaitable;
            return await func(arg1, arg2).ConfigureAwait(false);
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
        public static async Task<TResult> Run<T1, T2, T3, TResult>(Func<T1, T2, T3, Task<TResult>> func, T1 arg1, T2 arg2, T3 arg3)
        {
            await Task.SpawningAwaitable;
            return await func(arg1, arg2, arg3).ConfigureAwait(false);
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
        /// <typeparam name="T4">The type of the third argument to be passed to the function.</typeparam>
        /// <typeparam name="TResult">The result of the function.</typeparam>
        /// <returns>A task that represents a proxy for the task returned by function.</returns>
        public static async Task<TResult> Run<T1, T2, T3, T4, TResult>(Func<T1, T2, T3, T4, Task<TResult>> func, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            await Task.SpawningAwaitable;
            return await func(arg1, arg2, arg3, arg4).ConfigureAwait(false);
        }
    }
}