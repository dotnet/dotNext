using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
    /// <summary>
    /// Represents lightweight version of <see cref="Task"/>.
    /// </summary>
    /// <remarks>
    /// This data type is primarily used for bridging between synchronous or legacy asynchronous code into
    /// <c>await</c>-friendly form. It is NOT a replacement of <see cref="Task"/> in general.
    /// </remarks>
    /// <seealso href="https://en.wikipedia.org/wiki/Futures_and_promises">Futures and Promises</seealso>
    public abstract class Future : IFuture
    {
        /// <summary>
        /// Represents awaiter of the asynchronous computation result represented by future object.
        /// </summary>
        public interface IAwaiter : IFuture
        {
            /// <summary>
            /// Ends the wait for the completion of the asynchronous task.
            /// </summary>
            /// <exception cref="OperationCanceledException">Cancellation requested and caller specified that exception should be thrown.</exception>
            /// <exception cref="IncompletedFutureException">The current future is not completed.</exception>
            void GetResult();
        }

        /// <summary>
        /// Represents awaiter of the asynchronous computation result represented by future object.
        /// </summary>
        /// <typeparam name="R"></typeparam>
        public interface IAwaiter<out R> : IFuture
        {
            /// <summary>
            /// Ends the wait for the completion of the asynchronous task.
            /// </summary>
            /// <returns>The result of asynchronous computation.</returns>
            /// <exception cref="OperationCanceledException">Cancellation requested and caller specified that exception should be thrown.</exception>
            /// <exception cref="IncompletedFutureException">The current future is not completed.</exception>
            R GetResult();
        }

        /// <summary>
        /// Represents exception indicating that the result is requested from incompleted Future object.
        /// </summary>
        protected sealed class IncompletedFutureException : InvalidOperationException
        {
            /// <summary>
            /// Initializes a new exception.
            /// </summary>
            public IncompletedFutureException()
            {
            }
        }

        private sealed class Continuation
        {
            private static readonly Func<Action, Action> ContinuationWithoutContextFactory = DelegateHelpers.CreateClosedDelegateFactory<Action>(() => QueueContinuation(null));

            private readonly Action callback;
            private readonly SynchronizationContext context;

            private Continuation(Action callback, SynchronizationContext context)
            {
                this.callback = callback;
                this.context = context;
            }

            private static void Invoke(object continuation) => (continuation as Action)?.Invoke();

            //TODO: Should be replaced with typed QueueUserWorkItem in .NET Standard 2.1
            private static void QueueContinuation(Action callback) => ThreadPool.QueueUserWorkItem(Invoke, callback);

            private void Invoke() => context.Post(Invoke, callback);

            internal static Action Create(Action callback)
            {
                var context = SynchronizationContext.Current?.CreateCopy();
                return context is null ? ContinuationWithoutContextFactory(callback) : new Continuation(callback, context).Invoke;
            }
        }

        private Action continuation;

        /// <summary>
        /// Initializes a new Future.
        /// </summary>
        protected Future() { }

        /// <summary>
        /// Determines whether asynchronous operation referenced by this object is already completed.
        /// </summary>
        public abstract bool IsCompleted { get; }

        /// <summary>
        /// Moves this Future into completed state and execute all attached continuations.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        protected void Complete()
        {
            if (continuation is null)
                return;
            continuation();
            continuation = null;
        }

        /// <summary>
        /// Attaches the callback that will be invoked on completion.
        /// </summary>
        /// <param name="callback">The callback to be attached to the asynchronous operation which result is represented by this awaitable object.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void OnCompleted(Action callback)
        {
            if (IsCompleted)
                callback();
            else
                continuation += Continuation.Create(callback);
        }
    }

    /// <summary>
    /// Represents Future pattern that can be converted into <see cref="Task"/>.
    /// </summary>
    /// <typeparam name="T">The type of task that is supported by awaitable object.</typeparam>
    public abstract class Future<T> : Future
        where T : Task
    {
        /// <summary>
        /// Converts this awaitable object into task of type <typeparamref name="T"/>.
        /// </summary>
        /// <remarks>
        /// This method can cause extra allocation of memory. Do not use it for <c>await</c> scenario.
        /// It is suitable only for interop with <see cref="Task.WhenAll(System.Collections.Generic.IEnumerable{Task})"/>
        /// or <see cref="Task.WhenAny(System.Collections.Generic.IEnumerable{Task})"/>.
        /// </remarks>
        /// <returns>The task representing the current awaitable object.</returns>
        public abstract T AsTask();
    }
}
