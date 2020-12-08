using System;
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
    /// <typeparam name="TAwaiter">Any type implementing awaiter pattern.</typeparam>
    [Concept]
    public static class NotifyCompletion<TAwaiter>
        where TAwaiter : INotifyCompletion
    {
        private static readonly MemberGetter<TAwaiter, bool> IsCompletedImpl = Type<TAwaiter>.Property<bool>.RequireGetter(nameof(TaskAwaiter.IsCompleted))!;

        /// <summary>
        /// Gets a value that indicates whether the asynchronous task has completed.
        /// </summary>
        /// <param name="awaiter">An object that waits for the completion of an asynchronous task.</param>
        /// <returns><see langword="true"/> if the task has completed; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCompleted(in TAwaiter awaiter) => IsCompletedImpl(awaiter);

        /// <summary>
        /// Schedules the continuation action that's invoked when the instance completes.
        /// </summary>
        /// <param name="awaiter">An object that waits for the completion of an asynchronous task.</param>
        /// <param name="continuation">The action to invoke when the operation completes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OnCompleted(in TAwaiter awaiter, Action continuation)
            => Unsafe.AsRef(in awaiter).OnCompleted(continuation);
    }
}