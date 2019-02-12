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

		public static R GetResult<R>(this Task<R> task, CancellationToken token)
		{
			task.Wait(token);
			return task.Result;
		}

        public static async Task<(T1, T2)> WhenAll<T1, T2>(Task<T1> task1, Task<T2> task2) => (await task1, await task2);

        public static async Task<(T1, T2, T3)> WhenAll<T1, T2, T3>(Task<T1> task1, Task<T2> task2, Task<T3> task3) => (await task1, await task2, await task3);

        public static async Task<(T1, T2, T3, T4)> WhenAll<T1, T2, T3, T4>(Task<T1> task1, Task<T2> task2, Task<T3> task3, Task<T4> task4) => (await task1, await task2, await task3, await task4);

        public static async Task<(T1, T2, T3, T4, T5)> WhenAll<T1, T2, T3, T4, T5>(Task<T1> task1, Task<T2> task2, Task<T3> task3, Task<T4> task4, Task<T5> task5) => (await task1, await task2, await task3, await task4, await task5);
    }
}