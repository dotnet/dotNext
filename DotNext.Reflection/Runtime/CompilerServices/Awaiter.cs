using System;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices
{
    using Reflection;

    /// <summary>
    /// Represents base concept of awaiter pattern.
    /// </summary>
    /// <remarks>
    /// This concept doesn't provide methods to obtain task result.
    /// </remarks>
    /// <typeparam name="TAwaiter">Any type implementing awaiter pattern</typeparam>
    public abstract class AwaitableBase<TAwaiter>: IConcept<TAwaiter>
        where TAwaiter: ICriticalNotifyCompletion
    {
        private static readonly MemberGetter<TAwaiter, bool> isCompleted = Type<TAwaiter>.Property<bool>.GetGetter(nameof(TaskAwaiter.IsCompleted));

        private protected AwaitableBase() => throw new NotSupportedException();

        /// <summary>
        /// Gets a value that indicates whether the asynchronous task has completed.
        /// </summary>
        /// <param name="awaiter">An object that waits for the completion of an asynchronous task.</param>
        /// <returns><see langword="true"/> if the task has completed; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCompleted(in TAwaiter awaiter) => isCompleted(awaiter);

        /// <summary>
        /// Schedules the continuation action that's invoked when the instance completes.
        /// </summary>
        /// <param name="awaiter">An object that waits for the completion of an asynchronous task.</param>
        /// <param name="continutation">The action to invoke when the operation completes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UnsafeOnCompleted(in TAwaiter awaiter, Action continutation)
            => (Unsafe.AsRef(in awaiter)).UnsafeOnCompleted(continutation);

        /// <summary>
        /// Schedules the continuation action that's invoked when the instance completes.
        /// </summary>
        /// <param name="awaiter">An object that waits for the completion of an asynchronous task.</param>
        /// <param name="continutation">The action to invoke when the operation completes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OnCompleted(in TAwaiter awaiter, Action continutation)
            => (Unsafe.AsRef(in awaiter)).OnCompleted(continutation);
    }

    /// <summary>
    /// Represents awaiter pattern for type <typeparamref name="TAwaiter"/>
    /// with non-<see langword="void"/> result.
    /// </summary>
    /// <typeparam name="TAwaiter">Any type implementing awaiter pattern</typeparam>
    /// <typeparam name="R">Type of asynchronous result</typeparam>
    /// <see cref="Task{TResult}"/>
    /// <seealso cref="TaskAwaiter{TResult}"/>
    public sealed class Awaitable<TAwaiter, R>: AwaitableBase<TAwaiter>
        where TAwaiter: ICriticalNotifyCompletion
    {
        private Awaitable() => throw new NotSupportedException();

        private static readonly MemberGetter<TAwaiter, R> getResult = Type<TAwaiter>.Method.Get<MemberGetter<TAwaiter, R>>(nameof(TaskAwaiter<R>.GetResult), MethodLookup.Instance);

        /// <summary>
        /// Ends the wait for the completion of the asynchronous task.
        /// </summary>
        /// <param name="awaiter">An object that waits for the completion of an asynchronous task.</param>
        /// <returns>The result of the completed task.</returns>
        /// <exception cref="TaskCanceledException">The task was cancelled.</exception>
        /// <exception cref="Exception">Task is in faulted state.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R GetResult(in TAwaiter awaiter) => getResult(in awaiter);
    }

    /// <summary>
    /// Represents awaiter pattern for type <typeparamref name="TAwaiter"/>.
    /// with <see langword="void"/> result.
    /// </summary>
    /// <typeparam name="TAwaiter">Any type implementing awaiter pattern</typeparam>
    /// <seealso cref="TaskAwaiter"/>
    /// <seealso cref="Task"/>
    public sealed class Awaitable<TAwaiter>: AwaitableBase<TAwaiter>
        where TAwaiter: ICriticalNotifyCompletion
    {
        private delegate void GetResultMethod(in TAwaiter awaiter);

        private Awaitable() => throw new NotSupportedException();

        private static readonly GetResultMethod getResult = Type<TAwaiter>.Method.Get<GetResultMethod>(nameof(TaskAwaiter.GetResult), MethodLookup.Instance);

        /// <summary>
        /// Ends the wait for the completion of the asynchronous task.
        /// </summary>
        /// <param name="awaiter">An object that waits for the completion of an asynchronous task.</param>
        /// <exception cref="TaskCanceledException">The task was cancelled.</exception>
        /// <exception cref="Exception">Task is in faulted state.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetResult(in TAwaiter awaiter) => getResult(in awaiter);
    }
}