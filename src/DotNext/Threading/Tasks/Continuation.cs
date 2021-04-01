using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.Threading.Tasks
{
    using Generic;
    using static Runtime.Intrinsics;

    internal static class Continuation<T, TConstant>
        where TConstant : Constant<T>, new()
    {
        internal static readonly Func<Task<T>, object?, T> WhenFaulted = CompletedTask<T, TConstant>.WhenFaulted;
        internal static readonly Func<Task<T>, T> WhenCanceled = CompletedTask<T, TConstant>.WhenCanceled;
        internal static readonly Func<Task<T>, object?, T> WhenFaultedOrCanceled = CompletedTask<T, TConstant>.WhenFaultedOrCanceled;
    }

    /// <summary>
    /// Represents various continuations.
    /// </summary>
    public static class Continuation
    {
        private static readonly Action<Task, object?> WhenFaultedOrCanceledAction = WhenFaultedOrCanceled;

        private static void WhenFaultedOrCanceled(Task task, object? state)
            => task.ConfigureAwait(false).GetAwaiter().GetResult();

        [SuppressMessage("Design", "CA1068", Justification = "Method signature follows Task.ContinueWith")]
        private static Task<T> ContinueWithConstant<T, TConstant>(Task<T> task, bool completedSynchronously, Func<Task<T>, object?, T> continuation, Predicate<AggregateException> filter, CancellationToken token = default, TaskScheduler? scheduler = null)
            where TConstant : Constant<T>, new()
            => completedSynchronously ? CompletedTask<T, TConstant>.Task : task.ContinueWith(continuation, filter, token, TaskContinuationOptions.ExecuteSynchronously, scheduler ?? TaskScheduler.Current);

        /// <summary>
        /// Allows to obtain original <see cref="Task"/> in its final state after <c>await</c> without
        /// throwing exception produced by this task.
        /// </summary>
        /// <param name="task">The task to await.</param>
        /// <returns><paramref name="task"/> in final state.</returns>
        public static Task<Task> OnCompleted(this Task task)
            => task.ContinueWith(Func.Identity<Task>(), DefaultOf<CancellationToken>(), TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);

        /// <summary>
        /// Allows to obtain original <see cref="Task{R}"/> in its final state after <c>await</c> without
        /// throwing exception produced by this task.
        /// </summary>
        /// <typeparam name="TResult">The type of the task result.</typeparam>
        /// <param name="task">The task to await.</param>
        /// <returns><paramref name="task"/> in final state.</returns>
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
        public static Task<T> OnFaulted<T, TConstant>(this Task<T> task, TaskScheduler? scheduler = null)
            where TConstant : Constant<T>, new()
            => OnFaulted<T, TConstant>(task, Predicate.True<AggregateException>(), scheduler);

        /// <summary>
        /// Returns constant value if underlying task is failed with the exception that matches
        /// to the filter.
        /// </summary>
        /// <remarks>
        /// This continuation doesn't produce memory pressure. The delegate representing
        /// continuation is cached for future reuse as well as constant value.
        /// </remarks>
        /// <param name="task">The task to check.</param>
        /// <param name="filter">The exception filter.</param>
        /// <param name="scheduler">Optional scheduler used to schedule continuation.</param>
        /// <typeparam name="T">The type of task result.</typeparam>
        /// <typeparam name="TConstant">The type describing constant value.</typeparam>
        /// <returns>The task representing continuation.</returns>
        public static Task<T> OnFaulted<T, TConstant>(this Task<T> task, Predicate<AggregateException> filter, TaskScheduler? scheduler = null)
            where TConstant : Constant<T>, new()
            => ContinueWithConstant<T, TConstant>(task, task.IsFaulted && filter(task.Exception!), Continuation<T, TConstant>.WhenFaulted, filter, CancellationToken.None, scheduler);

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
        public static Task<T> OnFaultedOrCanceled<T, TConstant>(this Task<T> task, TaskScheduler? scheduler = null)
            where TConstant : Constant<T>, new()
            => OnFaultedOrCanceled<T, TConstant>(task, Predicate.True<AggregateException>(), scheduler);

        /// <summary>
        /// Returns constant value if underlying task is canceled or failed with the exception that matches
        /// to the filter.
        /// </summary>
        /// <remarks>
        /// This continuation doesn't produce memory pressure. The delegate representing
        /// continuation is cached for future reuse as well as constant value.
        /// </remarks>
        /// <param name="task">The task to check.</param>
        /// <param name="filter">The exception filter.</param>
        /// <param name="scheduler">Optional scheduler used to schedule continuation.</param>
        /// <typeparam name="T">The type of task result.</typeparam>
        /// <typeparam name="TConstant">The type describing constant value.</typeparam>
        /// <returns>The task representing continuation.</returns>
        public static Task<T> OnFaultedOrCanceled<T, TConstant>(this Task<T> task, Predicate<AggregateException> filter, TaskScheduler? scheduler = null)
            where TConstant : Constant<T>, new()
            => ContinueWithConstant<T, TConstant>(task, (task.IsFaulted && filter(task.Exception!)) || task.IsCanceled, Continuation<T, TConstant>.WhenFaultedOrCanceled, filter, CancellationToken.None, scheduler);

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
            => task.IsCanceled ? CompletedTask<T, TConstant>.Task : task.ContinueWith(Continuation<T, TConstant>.WhenCanceled, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, scheduler ?? TaskScheduler.Current);

        internal static void OnCompleted(this Task task, AsyncCallback callback)
            => task.ConfigureAwait(false).GetAwaiter().OnCompleted(() => callback(task));

        internal static Task AttachState(this Task task, object? state, CancellationToken token = default)
            => task.ContinueWith(WhenFaultedOrCanceledAction, state, token, TaskContinuationOptions.None, TaskScheduler.Default);

        private static async Task<T> WaitAsyncImpl<T>(Task<T> task, TimeSpan timeout, CancellationToken token)
        {
            using (var tokenSource = token.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(token) : new CancellationTokenSource())
            {
                if (ReferenceEquals(task, await Task.WhenAny(task, Task.Delay(timeout, tokenSource.Token)).ConfigureAwait(false)))
                {
                    tokenSource.Cancel();   // ensure that Delay task is cancelled
                    return await task.ConfigureAwait(false);
                }
            }

            token.ThrowIfCancellationRequested();
            throw new TimeoutException();
        }

        /// <summary>
        /// Attaches timeout and, optionally, token to the task.
        /// </summary>
        /// <param name="task">The source task.</param>
        /// <param name="timeout">The timeout of the asynchronous task.</param>
        /// <param name="token">The token that can be used to cancel the constructed task.</param>
        /// <typeparam name="T">The type of the task.</typeparam>
        /// <returns>The task with attached timeout and token.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="TimeoutException">The timeout has occurred.</exception>
        public static Task<T> ContinueWithTimeout<T>(this Task<T> task, TimeSpan timeout, CancellationToken token = default)
        {
            if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
                return Task.FromException<T>(new ArgumentOutOfRangeException(nameof(timeout)));
            if (token.IsCancellationRequested)
                return Task.FromCanceled<T>(token);
            if (task.IsCompleted)
                return task;
            if (timeout == TimeSpan.Zero)
                return Task.FromException<T>(new TimeoutException());
            if (timeout > InfiniteTimeSpan)
                return WaitAsyncImpl<T>(task, timeout, token);
            if (token.CanBeCanceled)
            {
                Ldnull();
                Ldftn(PropertyGet(Type<Task<T>>(), nameof(Task<T>.Result)));
                Newobj(Constructor(Type<Func<Task<T>, T>>(), Type<object>(), Type<IntPtr>()));
                Pop(out Func<Task<T>, T> continuation);
                return task.ContinueWith(continuation, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);
            }

            return task;
        }
    }
}