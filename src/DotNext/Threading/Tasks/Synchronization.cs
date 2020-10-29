using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading.Tasks
{
    using Generic;

    /// <summary>
    /// Represents task synchronization and combination methods.
    /// </summary>
    public static class Synchronization
    {
        private static readonly Func<Task, bool> TrueContinuation = task => true;

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
                result = task.Wait(timeout) ? task.Result : new Result<TResult>(new TimeoutException());
            }
            catch (Exception e)
            {
                result = new Result<TResult>(e);
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
            try
            {
                task.Wait(token);
                return task.Result;
            }
            catch (Exception e)
            {
                return new Result<TResult>(e);
            }
        }

        /// <summary>
        /// Gets task result synchronously.
        /// </summary>
        /// <param name="task">The task to synchronize.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task result; or <see cref="System.Reflection.Missing.Value"/> returned from <see cref="Result{T}.Value"/> if <paramref name="task"/> is not of type <see cref="Task{TResult}"/>.</returns>
        public static Result<dynamic?> GetResult(this Task task, CancellationToken token)
        {
            Result<object?> result;
            try
            {
                task.Wait(token);
                var awaiter = new DynamicTaskAwaitable.Awaiter(task, false);
                result = new Result<object?>(awaiter.GetRawResult());
            }
            catch (Exception e)
            {
                result = new Result<object?>(e);
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
        public static Result<dynamic?> GetResult(this Task task, TimeSpan timeout)
        {
            Result<dynamic?> result;
            try
            {
                if (task.Wait(timeout))
                {
                    var awaiter = new DynamicTaskAwaitable.Awaiter(task, false);
                    result = new Result<object?>(awaiter.GetRawResult());
                }
                else
                {
                    result = new Result<object?>(new TimeoutException());
                }
            }
            catch (Exception e)
            {
                result = new Result<object?>(e);
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
        public static async Task<(T1, T2)> WhenAll<T1, T2>(Task<T1> task1, Task<T2> task2) => (await task1.ConfigureAwait(false), await task2.ConfigureAwait(false));

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
        public static async Task<(T1, T2, T3)> WhenAll<T1, T2, T3>(Task<T1> task1, Task<T2> task2, Task<T3> task3) => (await task1.ConfigureAwait(false), await task2.ConfigureAwait(false), await task3.ConfigureAwait(false));

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
        public static async Task<(T1, T2, T3, T4)> WhenAll<T1, T2, T3, T4>(Task<T1> task1, Task<T2> task2, Task<T3> task3, Task<T4> task4) => (await task1.ConfigureAwait(false), await task2.ConfigureAwait(false), await task3.ConfigureAwait(false), await task4.ConfigureAwait(false));

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
        public static async Task<(T1, T2, T3, T4, T5)> WhenAll<T1, T2, T3, T4, T5>(Task<T1> task1, Task<T2> task2, Task<T3> task3, Task<T4> task4, Task<T5> task5) => (await task1.ConfigureAwait(false), await task2.ConfigureAwait(false), await task3.ConfigureAwait(false), await task4.ConfigureAwait(false), await task5.ConfigureAwait(false));

        private static async Task<bool> WaitAsyncImpl(Task task, TimeSpan timeout, CancellationToken token)
        {
            using (var tokenSource = token.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(token) : new CancellationTokenSource())
            {
                if (ReferenceEquals(task, await Task.WhenAny(task, Task.Delay(timeout, tokenSource.Token)).ConfigureAwait(false)))
                {
                    tokenSource.Cancel();   // ensure that Delay task is cancelled
                    return true;
                }
            }

            token.ThrowIfCancellationRequested();
            return false;
        }

        /// <summary>
        /// Waits for task completion asynchronously.
        /// </summary>
        /// <param name="task">The task to await.</param>
        /// <param name="timeout">The time to wait for task completion.</param>
        /// <param name="token">The token that can be used to cancel awaiting.</param>
        /// <returns><see langword="true"/> if task is completed; <see langword="false"/> if task is not completed.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static Task<bool> WaitAsync(this Task task, TimeSpan timeout, CancellationToken token = default)
        {
            if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            if (token.IsCancellationRequested)
                return Task.FromCanceled<bool>(token);
            if (task.IsCompleted)
                return CompletedTask<bool, BooleanConst.True>.Task;
            if (timeout == TimeSpan.Zero)
                return CompletedTask<bool, BooleanConst.False>.Task;    // if timeout is zero fail fast
            if (timeout > InfiniteTimeSpan)
                return WaitAsyncImpl(task, timeout, token);
            return !token.CanBeCanceled && task is Task<bool> boolTask ? boolTask : task.ContinueWith(TrueContinuation, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);
        }
    }
}