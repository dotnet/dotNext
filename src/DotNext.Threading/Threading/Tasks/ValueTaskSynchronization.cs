using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
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
        /// <summary>
        /// Creates a task that will complete when all of the passed tasks have completed.
        /// </summary>
        /// <remarks>
        /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
        /// </remarks>
        /// <param name="task1">The first task to await.</param>
        /// <param name="task2">The second task to await.</param>
        /// <returns>A task that represents the completion of all of the supplied tasks.</returns>
        public static async ValueTask WhenAll(ValueTask task1, ValueTask task2)
        {
            await task1.ConfigureAwait(false);
            await task2.ConfigureAwait(false);
        }

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
        public static async ValueTask<(T1, T2)> WhenAll<T1, T2>(ValueTask<T1> task1, ValueTask<T2> task2) => (await task1.ConfigureAwait(false), await task2.ConfigureAwait(false));

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
        public static async ValueTask WhenAll(ValueTask task1, ValueTask task2, ValueTask task3)
        {
            await task1.ConfigureAwait(false);
            await task2.ConfigureAwait(false);
            await task3.ConfigureAwait(false);
        }

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
        public static async ValueTask<(T1, T2, T3)> WhenAll<T1, T2, T3>(ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3) => (await task1.ConfigureAwait(false), await task2.ConfigureAwait(false), await task3.ConfigureAwait(false));

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
        public static async ValueTask WhenAll(ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4)
        {
            await task1.ConfigureAwait(false);
            await task2.ConfigureAwait(false);
            await task3.ConfigureAwait(false);
            await task4.ConfigureAwait(false);
        }

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
        public static async ValueTask<(T1, T2, T3, T4)> WhenAll<T1, T2, T3, T4>(ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4) => (await task1.ConfigureAwait(false), await task2.ConfigureAwait(false), await task3.ConfigureAwait(false), await task4.ConfigureAwait(false));

        /// <summary>
        /// Creates a task that will complete when all of the passed tasks have completed.
        /// </summary>
        /// <remarks>
        /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
        /// </remarks>
        /// <param name="task1">The first task to await.</param>
        /// <param name="task2">The second task to await.</param>
        /// <param name="task3">The third task to await.</param>
        /// <param name="task4">The third task to await.</param>
        /// <param name="task5">The fifth task to await.</param>
        /// <returns>A task that represents the completion of all of the supplied tasks.</returns>
        public static async ValueTask WhenAll(ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5)
        {
            await task1.ConfigureAwait(false);
            await task2.ConfigureAwait(false);
            await task3.ConfigureAwait(false);
            await task4.ConfigureAwait(false);
            await task5.ConfigureAwait(false);
        }

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
        public static async ValueTask<(T1, T2, T3, T4, T5)> WhenAll<T1, T2, T3, T4, T5>(ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5) => (await task1.ConfigureAwait(false), await task2.ConfigureAwait(false), await task3.ConfigureAwait(false), await task4.ConfigureAwait(false), await task5.ConfigureAwait(false));

        /// <summary>
        /// Creates a task that will complete when any of the supplied tasks have completed.
        /// </summary>
        /// <remarks>
        /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
        /// </remarks>
        /// <param name="task1">The first task to wait on for completion.</param>
        /// <param name="task2">The second task to wait on for completion.</param>
        /// <returns>A task that represents the completion of one of the supplied tasks. The return task's <see cref="ValueTask{TResult}.Result"/> is the task that completed.</returns>
        public static ValueTask<ValueTask> WhenAny(ValueTask task1, ValueTask task2)
        {
            if (task1.IsCompleted)
                return new ValueTask<ValueTask>(task1);
            else if (task2.IsCompleted)
                return new ValueTask<ValueTask>(task2);
            var whenAny = new ValueTaskCompletionSource2(task1, task2);
            task1.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFirst);
            task2.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteSecond);
            return whenAny.Task;
        }

        /// <summary>
        /// Creates a task that will complete when any of the supplied tasks have completed.
        /// </summary>
        /// <remarks>
        /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
        /// </remarks>
        /// <param name="task1">The first task to wait on for completion.</param>
        /// <param name="task2">The second task to wait on for completion.</param>
        /// <typeparam name="R">The type of the result produced by the tasks.</typeparam>
        /// <returns>A task that represents the completion of one of the supplied tasks. The return task's <see cref="ValueTask{TResult}.Result"/> is the task that completed.</returns>
        public static ValueTask<ValueTask<R>> WhenAny<R>(ValueTask<R> task1, ValueTask<R> task2)
        {
            if (task1.IsCompleted)
                return new ValueTask<ValueTask<R>>(task1);
            else if (task2.IsCompleted)
                return new ValueTask<ValueTask<R>>(task2);
            var whenAny = new ValueTaskCompletionSource2<R>(task1, task2);
            task1.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFirst);
            task2.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteSecond);
            return whenAny.Task;
        }

        /// <summary>
        /// Creates a task that will complete when any of the supplied tasks have completed.
        /// </summary>
        /// <remarks>
        /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
        /// </remarks>
        /// <param name="task1">The first task to wait on for completion.</param>
        /// <param name="task2">The second task to wait on for completion.</param>
        /// <param name="task3">The third task to wait on for completion.</param>
        /// <returns>A task that represents the completion of one of the supplied tasks. The return task's <see cref="ValueTask{TResult}.Result"/> is the task that completed.</returns>
        public static ValueTask<ValueTask> WhenAny(ValueTask task1, ValueTask task2, ValueTask task3)
        {
            if (task1.IsCompleted)
                return new ValueTask<ValueTask>(task1);
            else if (task2.IsCompleted)
                return new ValueTask<ValueTask>(task2);
            else if (task3.IsCompleted)
                return new ValueTask<ValueTask>(task3);
            var whenAny = new ValueTaskCompletionSource3(task1, task2, task3);
            task1.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFirst);
            task2.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteSecond);
            task3.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteThird);
            return whenAny.Task;
        }

        /// <summary>
        /// Creates a task that will complete when any of the supplied tasks have completed.
        /// </summary>
        /// <remarks>
        /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
        /// </remarks>
        /// <param name="task1">The first task to wait on for completion.</param>
        /// <param name="task2">The second task to wait on for completion.</param>
        /// <param name="task3">The third task to wait on for completion.</param>
        /// <typeparam name="R">The type of the result produced by the tasks.</typeparam>
        /// <returns>A task that represents the completion of one of the supplied tasks. The return task's <see cref="ValueTask{TResult}.Result"/> is the task that completed.</returns>
        public static ValueTask<ValueTask<R>> WhenAny<R>(ValueTask<R> task1, ValueTask<R> task2, ValueTask<R> task3)
        {
            if (task1.IsCompleted)
                return new ValueTask<ValueTask<R>>(task1);
            else if (task2.IsCompleted)
                return new ValueTask<ValueTask<R>>(task2);
            else if (task3.IsCompleted)
                return new ValueTask<ValueTask<R>>(task3);
            var whenAny = new ValueTaskCompletionSource3<R>(task1, task2, task3);
            task1.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFirst);
            task2.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteSecond);
            task3.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteThird);
            return whenAny.Task;
        }

        /// <summary>
        /// Creates a task that will complete when any of the supplied tasks have completed.
        /// </summary>
        /// <remarks>
        /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
        /// </remarks>
        /// <param name="task1">The first task to wait on for completion.</param>
        /// <param name="task2">The second task to wait on for completion.</param>
        /// <param name="task3">The third task to wait on for completion.</param>
        /// <param name="task4">The fourth task to wait on for completion.</param>
        /// <returns>A task that represents the completion of one of the supplied tasks. The return task's <see cref="ValueTask{TResult}.Result"/> is the task that completed.</returns>
        public static ValueTask<ValueTask> WhenAny(ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4)
        {
            if (task1.IsCompleted)
                return new ValueTask<ValueTask>(task1);
            else if (task2.IsCompleted)
                return new ValueTask<ValueTask>(task2);
            else if (task3.IsCompleted)
                return new ValueTask<ValueTask>(task3);
            else if (task4.IsCompleted)
                return new ValueTask<ValueTask>(task4);
            var whenAny = new ValueTaskCompletionSource4(task1, task2, task3, task4);
            task1.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFirst);
            task2.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteSecond);
            task3.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteThird);
            task4.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFourth);
            return whenAny.Task;
        }

        /// <summary>
        /// Creates a task that will complete when any of the supplied tasks have completed.
        /// </summary>
        /// <remarks>
        /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
        /// </remarks>
        /// <param name="task1">The first task to wait on for completion.</param>
        /// <param name="task2">The second task to wait on for completion.</param>
        /// <param name="task3">The third task to wait on for completion.</param>
        /// <param name="task4">The fourth task to wait on for completion.</param>
        /// <typeparam name="R">The type of the result produced by the tasks.</typeparam>
        /// <returns>A task that represents the completion of one of the supplied tasks. The return task's <see cref="ValueTask{TResult}.Result"/> is the task that completed.</returns>
        public static ValueTask<ValueTask<R>> WhenAny<R>(ValueTask<R> task1, ValueTask<R> task2, ValueTask<R> task3, ValueTask<R> task4)
        {
            if (task1.IsCompleted)
                return new ValueTask<ValueTask<R>>(task1);
            else if (task2.IsCompleted)
                return new ValueTask<ValueTask<R>>(task2);
            else if (task3.IsCompleted)
                return new ValueTask<ValueTask<R>>(task3);
            else if (task4.IsCompleted)
                return new ValueTask<ValueTask<R>>(task4);
            var whenAny = new ValueTaskCompletionSource4<R>(task1, task2, task3, task4);
            task1.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFirst);
            task2.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteSecond);
            task3.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteThird);
            task4.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFourth);
            return whenAny.Task;
        }

        /// <summary>
        /// Creates a task that will complete when any of the supplied tasks have completed.
        /// </summary>
        /// <remarks>
        /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
        /// </remarks>
        /// <param name="task1">The first task to wait on for completion.</param>
        /// <param name="task2">The second task to wait on for completion.</param>
        /// <param name="task3">The third task to wait on for completion.</param>
        /// <param name="task4">The fourth task to wait on for completion.</param>
        /// <param name="task5">The fifth task to wait on for completion.</param>
        /// <returns>A task that represents the completion of one of the supplied tasks. The return task's <see cref="ValueTask{TResult}.Result"/> is the task that completed.</returns>
        public static ValueTask<ValueTask> WhenAny(ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5)
        {
            if (task1.IsCompleted)
                return new ValueTask<ValueTask>(task1);
            else if (task2.IsCompleted)
                return new ValueTask<ValueTask>(task2);
            else if (task3.IsCompleted)
                return new ValueTask<ValueTask>(task3);
            else if (task4.IsCompleted)
                return new ValueTask<ValueTask>(task4);
            else if (task5.IsCompleted)
                return new ValueTask<ValueTask>(task5);
            var whenAny = new ValueTaskCompletionSource5(task1, task2, task3, task4, task5);
            task1.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFirst);
            task2.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteSecond);
            task3.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteThird);
            task4.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFourth);
            task5.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFifth);
            return whenAny.Task;
        }

        /// <summary>
        /// Creates a task that will complete when any of the supplied tasks have completed.
        /// </summary>
        /// <remarks>
        /// This method avoid memory allocation in the managed heap if all tasks are completed (or will be soon) at the time of calling this method.
        /// </remarks>
        /// <param name="task1">The first task to wait on for completion.</param>
        /// <param name="task2">The second task to wait on for completion.</param>
        /// <param name="task3">The third task to wait on for completion.</param>
        /// <param name="task4">The fourth task to wait on for completion.</param>
        /// <param name="task5">The fourth task to wait on for completion.</param>
        /// <typeparam name="R">The type of the result produced by the tasks.</typeparam>
        /// <returns>A task that represents the completion of one of the supplied tasks. The return task's <see cref="ValueTask{TResult}.Result"/> is the task that completed.</returns>
        public static ValueTask<ValueTask<R>> WhenAny<R>(ValueTask<R> task1, ValueTask<R> task2, ValueTask<R> task3, ValueTask<R> task4, ValueTask<R> task5)
        {
            if (task1.IsCompleted)
                return new ValueTask<ValueTask<R>>(task1);
            else if (task2.IsCompleted)
                return new ValueTask<ValueTask<R>>(task2);
            else if (task3.IsCompleted)
                return new ValueTask<ValueTask<R>>(task3);
            else if (task4.IsCompleted)
                return new ValueTask<ValueTask<R>>(task4);
            else if (task5.IsCompleted)
                return new ValueTask<ValueTask<R>>(task5);
            var whenAny = new ValueTaskCompletionSource5<R>(task1, task2, task3, task4, task5);
            task1.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFirst);
            task2.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteSecond);
            task3.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteThird);
            task4.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFourth);
            task5.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFifth);
            return whenAny.Task;
        }
    }
}