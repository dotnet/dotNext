using System;
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
    /// <typeparam name="TAwaiter">Any type implementing awaiter pattern</typeparam>
    /// <typeparam name="R">Type of asynchronous result</typeparam>
    /// <seealso cref="Task{TResult}"/>
    /// <seealso cref="TaskAwaiter{TResult}"/>
    [Concept]
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Awaiter<[Constraint(typeof(NotifyCompletion<>))]TAwaiter, R> : INotifyCompletion
        where TAwaiter : INotifyCompletion
    {
        private static readonly MemberGetter<TAwaiter, R> getResult = Type<TAwaiter>.Method.Get<MemberGetter<TAwaiter, R>>(nameof(TaskAwaiter<R>.GetResult), MethodLookup.Instance);

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
        public R GetResult() => GetResult(in awaiter);

        /// <summary>
        /// Gets a value that indicates whether the asynchronous task has completed.
        /// </summary>
        public bool IsCompleted => NotifyCompletion<TAwaiter>.IsCompleted(in awaiter);

        void INotifyCompletion.OnCompleted(Action continuation) => awaiter.OnCompleted(continuation);

        /// <summary>
        /// Extracts underlying awaiter object from this wrapper.
        /// </summary>
        /// <param name="awaiter">Generic awaiter object.</param>
        public static implicit operator TAwaiter(in Awaiter<TAwaiter, R> awaiter) => awaiter.awaiter;

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
    /// with <see cref="void"/> result.
    /// </summary>
    /// <typeparam name="TAwaiter">Any type implementing awaiter pattern</typeparam>
    /// <seealso cref="TaskAwaiter"/>
    /// <seealso cref="Task"/>
    [Concept]
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Awaiter<[Constraint(typeof(NotifyCompletion<>))]TAwaiter> : INotifyCompletion
        where TAwaiter : INotifyCompletion
    {
        private delegate void GetResultMethod(in TAwaiter awaiter);

        static Awaiter() => Concept.Assert(typeof(NotifyCompletion<TAwaiter>));

        private static readonly GetResultMethod getResult = Type<TAwaiter>.Method.Get<GetResultMethod>(nameof(TaskAwaiter.GetResult), MethodLookup.Instance);

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
        public static void GetResult(in TAwaiter awaiter) => getResult(in awaiter);
    }
}