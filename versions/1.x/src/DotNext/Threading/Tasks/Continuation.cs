using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
    using Generic;
    using static Runtime.Intrinsics;

    internal static class Continuation<T, C>
        where C : Constant<T>, new()
    {
        internal static readonly Func<Task<T>, T> WhenFaulted = CompletedTask<T, C>.WhenFaulted;
        internal static readonly Func<Task<T>, T> WhenCanceled = CompletedTask<T, C>.WhenCanceled;
        internal static readonly Func<Task<T>, T> WhenFaultedOrCanceled = CompletedTask<T, C>.WhenFaultedOrCanceled;
    }

    /// <summary>
    /// Represents various continuations.
    /// </summary>
    public static class Continuation
    {
        [SuppressMessage("Design", "CA1068", Justification = "Signature is similar to ContinueWith method")]
        private static Task<T> ContinueWithConstant<T, C>(Task<T> task, bool completedSynchronously, Func<Task<T>, T> continuation, CancellationToken token = default, TaskScheduler scheduler = null)
            where C : Constant<T>, new()
            => completedSynchronously ? CompletedTask<T, C>.Task : task.ContinueWith(continuation, token, TaskContinuationOptions.ExecuteSynchronously, scheduler ?? TaskScheduler.Current);

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
        /// <typeparam name="R">The type of the task result.</typeparam>
        /// <param name="task">The task to await.</param>
        /// <returns><paramref name="task"/> in final state.</returns>
        [SuppressMessage("Design", "CA1068", Justification = "Signature is similar to ContinueWith method")]
        public static Task<Task<R>> OnCompleted<R>(this Task<R> task)
            => task.ContinueWith(Func.Identity<Task<R>>(), DefaultOf<CancellationToken>(), TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);

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
        /// <typeparam name="C">The type describing constant value.</typeparam>
        /// <returns>The task representing continuation.</returns>
        [SuppressMessage("Design", "CA1068", Justification = "Signature is similar to ContinueWith method")]
        public static Task<T> OnFaulted<T, C>(this Task<T> task, TaskScheduler scheduler = null)
            where C : Constant<T>, new()
            => ContinueWithConstant<T, C>(task, task.IsFaulted, Continuation<T, C>.WhenFaulted, DefaultOf<CancellationToken>(), scheduler);

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
        /// <typeparam name="C">The type describing constant value.</typeparam>
        /// <returns>The task representing continuation.</returns>        
        [SuppressMessage("Design", "CA1068", Justification = "Signature is similar to ContinueWith method")]
        public static Task<T> OnFaultedOrCanceled<T, C>(this Task<T> task, TaskScheduler scheduler = null)
            where C : Constant<T>, new()
            => ContinueWithConstant<T, C>(task, task.IsFaulted | task.IsCanceled, Continuation<T, C>.WhenFaultedOrCanceled, DefaultOf<CancellationToken>(), scheduler);

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
        /// <typeparam name="C">The type describing constant value.</typeparam>
        /// <returns>The task representing continuation.</returns>
        [SuppressMessage("Design", "CA1068", Justification = "Signature is similar to ContinueWith method")]
        public static Task<T> OnCanceled<T, C>(this Task<T> task, TaskScheduler scheduler = null)
            where C : Constant<T>, new()
            => ContinueWithConstant<T, C>(task, task.IsCanceled, Continuation<T, C>.WhenCanceled, DefaultOf<CancellationToken>(), scheduler);
    }
}