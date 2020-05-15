using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
    using Generic;
    using static Runtime.Intrinsics;

    internal static class Continuation<T, TConstant>
        where TConstant : Constant<T>, new()
    {
        internal static readonly Func<Task<T>, T> WhenFaulted = CompletedTask<T, TConstant>.WhenFaulted;
        internal static readonly Func<Task<T>, T> WhenCanceled = CompletedTask<T, TConstant>.WhenCanceled;
        internal static readonly Func<Task<T>, T> WhenFaultedOrCanceled = CompletedTask<T, TConstant>.WhenFaultedOrCanceled;
    }

    /// <summary>
    /// Represents various continuations.
    /// </summary>
    public static class Continuation
    {
        [SuppressMessage("Design", "CA1068", Justification = "Symmetry with ContinueWith method")]
        private static Task<T> ContinueWithConstant<T, TConstant>(Task<T> task, bool completedSynchronously, Func<Task<T>, T> continuation, CancellationToken token = default, TaskScheduler? scheduler = null)
            where TConstant : Constant<T>, new()
            => completedSynchronously ? CompletedTask<T, TConstant>.Task : task.ContinueWith(continuation, token, TaskContinuationOptions.ExecuteSynchronously, scheduler ?? TaskScheduler.Current);

        /// <summary>
        /// Allows to obtain original <see cref="Task"/> in its final state after <c>await</c> without
        /// throwing exception produced by this task.
        /// </summary>
        /// <param name="task">The task to await.</param>
        /// <returns><paramref name="task"/> in final state.</returns>
        [SuppressMessage("Design", "CA1068", Justification = "Signature is similar to ContinueWith method")]
        public static Task<Task> OnCompleted(this Task task)
            => task.ContinueWith(Func.Identity<Task>(), DefaultOf<CancellationToken>(), TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);

        /// <summary>
        /// Allows to obtain original <see cref="Task{R}"/> in its final state after <c>await</c> without
        /// throwing exception produced by this task.
        /// </summary>
        /// <typeparam name="TResult">The type of the task result.</typeparam>
        /// <param name="task">The task to await.</param>
        /// <returns><paramref name="task"/> in final state.</returns>
        [SuppressMessage("Design", "CA1068", Justification = "Signature is similar to ContinueWith method")]
        public static Task<Task<TResult>> OnCompleted<TResult>(this Task<TResult> task)
            => task.ContinueWith(Func.Identity<Task<TResult>>(), DefaultOf<CancellationToken>(), TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);

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
        [SuppressMessage("Design", "CA1068", Justification = "Signature is similar to ContinueWith method")]
        public static Task<T> OnFaulted<T, TConstant>(this Task<T> task, TaskScheduler? scheduler = null)
            where TConstant : Constant<T>, new()
            => ContinueWithConstant<T, TConstant>(task, task.IsFaulted, Continuation<T, TConstant>.WhenFaulted, DefaultOf<CancellationToken>(), scheduler);

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
        [SuppressMessage("Design", "CA1068", Justification = "Signature is similar to ContinueWith method")]
        public static Task<T> OnFaultedOrCanceled<T, TConstant>(this Task<T> task, TaskScheduler? scheduler = null)
            where TConstant : Constant<T>, new()
            => ContinueWithConstant<T, TConstant>(task, task.IsFaulted | task.IsCanceled, Continuation<T, TConstant>.WhenFaultedOrCanceled, DefaultOf<CancellationToken>(), scheduler);

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
            => ContinueWithConstant<T, TConstant>(task, task.IsCanceled, Continuation<T, TConstant>.WhenCanceled, DefaultOf<CancellationToken>(), scheduler);

        internal static void OnCompleted(this Task task, AsyncCallback callback)
            => task.ConfigureAwait(false).GetAwaiter().OnCompleted(() => callback(task));
    }
}