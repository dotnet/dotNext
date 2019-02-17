using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
    /// <summary>
    /// Represents task synchronization methods.
    /// </summary>
    public static class Synchronization
    {
        /// <summary>
        /// Gets task result synchronously.
        /// </summary>
        /// <param name="task">The task to synchronize.</param>
        /// <param name="timeout">Synchronization timeout.</param>
        /// <typeparam name="R">Type of task result.</typeparam>
        /// <returns>Task result.</returns>
        /// <exception cref="TimeoutException">Task is not completed.</exception>
        public static R GetResult<R>(this Task<R> task, TimeSpan timeout)
            => task.Wait(timeout) ? task.Result : throw new TimeoutException();

        /// <summary>
        /// Gets task result synchronously.
        /// </summary>
        /// <param name="task">The task to synchronize.</param>
        /// <param name="token">Cancellation token.</param>
        /// <typeparam name="R">Type of task result.</typeparam>
        /// <returns>Task result.</returns>
        /// <exception cref="TimeoutException">Task is not completed.</exception>
        public static R GetResult<R>(this Task<R> task, CancellationToken token)
		{
			task.Wait(token);
			return task.Result;
		}

        /// <summary>
        /// Creates a task that will complete when all of the passed tasks have completed.
        /// </summary>
        /// <typeparam name="T1">The type of the first task.</typeparam>
        /// <typeparam name="T2">The type of the second task.</typeparam>
        /// <param name="task1">The first task to await.</param>
        /// <param name="task2">The second task to await.</param>
        /// <returns>The task containing results of both tasks.</returns>
        public static async Task<(T1, T2)> WhenAll<T1, T2>(Task<T1> task1, Task<T2> task2) => (await task1, await task2);
        
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
        public static async Task<(T1, T2, T3)> WhenAll<T1, T2, T3>(Task<T1> task1, Task<T2> task2, Task<T3> task3) => (await task1, await task2, await task3);

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
        public static async Task<(T1, T2, T3, T4)> WhenAll<T1, T2, T3, T4>(Task<T1> task1, Task<T2> task2, Task<T3> task3, Task<T4> task4) => (await task1, await task2, await task3, await task4);

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
        public static async Task<(T1, T2, T3, T4, T5)> WhenAll<T1, T2, T3, T4, T5>(Task<T1> task1, Task<T2> task2, Task<T3> task3, Task<T4> task4, Task<T5> task5) => (await task1, await task2, await task3, await task4, await task5);
    }
}