using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DotNext.Runtime.CompilerServices
{
    using Reflection;

    /// <summary>
    /// Represents awaiter pattern for type <typeparamref name="TAwaiter"/>
    /// with non-<see cref="void"/> result.
    /// </summary>
    /// <typeparam name="TAwaiter">Any type implementing awaiter pattern.</typeparam>
    /// <typeparam name="TResult">Type of asynchronous result.</typeparam>
    /// <seealso cref="Task{TResult}"/>
    /// <seealso cref="TaskAwaiter{TResult}"/>
    [Concept]
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Awaiter<[Constraint(typeof(NotifyCompletion<>))]TAwaiter, TResult> : INotifyCompletion
        where TAwaiter : INotifyCompletion
    {
        private static readonly MemberGetter<TAwaiter, TResult> GetResultMethod = Type<TAwaiter>.Method.Require<MemberGetter<TAwaiter, TResult>>(nameof(TaskAwaiter<TResult>.GetResult), MethodLookup.Instance)!;

        static Awaiter() => Concept.Assert(typeof(NotifyCompletion<TAwaiter>));

        private readonly TAwaiter awaiter;

        /// <summary>
        /// Initializes a new generic awaiter object.
        /// </summary>
        /// <param name="awaiter">Underlying awaiter object.</param>
        public Awaiter(TAwaiter awaiter) => this.awaiter = awaiter;

        /// <summary>
        /// Ends the wait for the completion of the asynchronous task.
        /// </summary>
        /// <returns>The result of the completed task.</returns>
        /// <exception cref="TaskCanceledException">The task was cancelled.</exception>
        /// <exception cref="Exception">Task is in faulted state.</exception>
        [return: MaybeNull]
        public TResult GetResult() => GetResult(in awaiter);

        /// <summary>
        /// Gets a value that indicates whether the asynchronous task has completed.
        /// </summary>
        public bool IsCompleted => NotifyCompletion<TAwaiter>.IsCompleted(in awaiter);

        /// <inheritdoc/>
        void INotifyCompletion.OnCompleted(Action continuation) => awaiter.OnCompleted(continuation);

        /// <summary>
        /// Extracts underlying awaiter object from this wrapper.
        /// </summary>
        /// <param name="awaiter">Generic awaiter object.</param>
        public static implicit operator TAwaiter(in Awaiter<TAwaiter, TResult> awaiter) => awaiter.awaiter;

        /// <summary>
        /// Ends the wait for the completion of the asynchronous task.
        /// </summary>
        /// <param name="awaiter">An object that waits for the completion of an asynchronous task.</param>
        /// <returns>The result of the completed task.</returns>
        /// <exception cref="TaskCanceledException">The task was cancelled.</exception>
        /// <exception cref="Exception">Task is in faulted state.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: MaybeNull]
        public static TResult GetResult(in TAwaiter awaiter) => GetResultMethod(in awaiter);
    }

    /// <summary>
    /// Represents awaiter pattern for type <typeparamref name="TAwaiter"/>.
    /// with <see cref="void"/> result.
    /// </summary>
    /// <typeparam name="TAwaiter">Any type implementing awaiter pattern.</typeparam>
    /// <seealso cref="TaskAwaiter"/>
    /// <seealso cref="Task"/>
    [Concept]
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Awaiter<[Constraint(typeof(NotifyCompletion<>))]TAwaiter> : INotifyCompletion
        where TAwaiter : INotifyCompletion
    {
        private delegate void GetResultMethod(in TAwaiter awaiter);

        static Awaiter() => Concept.Assert(typeof(NotifyCompletion<TAwaiter>));

        private static readonly GetResultMethod GetResultImpl = Type<TAwaiter>.Method.Require<GetResultMethod>(nameof(TaskAwaiter.GetResult), MethodLookup.Instance)!;

        private readonly TAwaiter awaiter;

        /// <summary>
        /// Initializes a new generic awaiter object.
        /// </summary>
        /// <param name="awaiter">Underlying awaiter object.</param>
        public Awaiter(TAwaiter awaiter) => this.awaiter = awaiter;

        /// <summary>
        /// Ends the wait for the completion of the asynchronous task.
        /// </summary>
        /// <exception cref="TaskCanceledException">The task was cancelled.</exception>
        /// <exception cref="Exception">Task is in faulted state.</exception>
        public void GetResult() => GetResult(in awaiter);

        /// <summary>
        /// Gets a value that indicates whether the asynchronous task has completed.
        /// </summary>
        public bool IsCompleted => NotifyCompletion<TAwaiter>.IsCompleted(in awaiter);

        /// <inheritdoc/>
        void INotifyCompletion.OnCompleted(Action continuation) => awaiter.OnCompleted(continuation);

        /// <summary>
        /// Extracts underlying awaiter object from this wrapper.
        /// </summary>
        /// <param name="awaiter">Generic awaiter object.</param>
        public static implicit operator TAwaiter(in Awaiter<TAwaiter> awaiter) => awaiter.awaiter;

        /// <summary>
        /// Ends the wait for the completion of the asynchronous task.
        /// </summary>
        /// <param name="awaiter">An object that waits for the completion of an asynchronous task.</param>
        /// <exception cref="TaskCanceledException">The task was cancelled.</exception>
        /// <exception cref="Exception">Task is in faulted state.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetResult(in TAwaiter awaiter) => GetResultImpl(in awaiter);
    }
}