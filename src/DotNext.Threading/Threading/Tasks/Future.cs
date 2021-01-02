using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace DotNext.Threading.Tasks
{
    /// <summary>
    /// Represents lightweight version of <see cref="Task"/>.
    /// </summary>
    public abstract class Future : IFuture, IValueTaskSource
    {
        /// <summary>
        /// Represents awaiter of the asynchronous computation result represented by future object.
        /// </summary>
        public interface IAwaiter
        {
            /// <summary>
            /// Ends the wait for the completion of the asynchronous task.
            /// </summary>
            void GetResult();
        }

        private readonly ValueTaskSourceOnCompletedFlags flags;
        private ManualResetValueTaskSourceCore<Missing> source;

        /// <summary>
        /// Initializes a new future.
        /// </summary>
        /// <param name="runContinuationsAsynchronously"><see langword="true"/> to force continuations to run asynchronously; otherwise, <see langword="false"/>.</param>
        /// <param name="continueOnCapturedContext"><see langword="true"/> to attempt to marshal the continuation back to the original context captured; otherwise, <see langword="false"/>.</param>
        protected Future(bool runContinuationsAsynchronously = true, bool continueOnCapturedContext = false)
        {
            source = new ManualResetValueTaskSourceCore<Missing>
            {
                RunContinuationsAsynchronously = runContinuationsAsynchronously,
            };

            flags = continueOnCapturedContext ? ValueTaskSourceOnCompletedFlags.UseSchedulingContext : ValueTaskSourceOnCompletedFlags.None;
        }

        /// <summary>
        /// Determines whether asynchronous operation referenced by this object is already completed.
        /// </summary>
        public bool IsCompleted => source.GetStatus(source.Version) != ValueTaskSourceStatus.Pending;

        /// <summary>
        /// Completes this future.
        /// </summary>
        /// <param name="e"><see langword="null"/> to complete the future successfully; otherwise, pass the error.</param>
        protected void Complete(Exception? e = null)
        {
            if (e is null)
                source.SetResult(Missing.Value);
            else
                source.SetException(e);
        }

        /// <summary>
        /// Attaches the callback that will be invoked on completion.
        /// </summary>
        /// <param name="callback">The callback to be attached to the asynchronous operation which result is represented by this awaitable object.</param>
        public void OnCompleted(Action callback)
            => source.OnCompleted(callback, source.Version, flags);

        /// <summary>
        /// Gets value associated with this future.
        /// </summary>
        protected void GetResult() => source.GetResult(source.Version);

        /// <summary>
        /// Converts this future to the task.
        /// </summary>
        /// <returns>The task representing this future.</returns>
        public ValueTask AsTask() => new ValueTask(this, source.Version);

        /// <inheritdoc />
        void IValueTaskSource.GetResult(short token) => source.GetResult(token);

        /// <inheritdoc />
        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) => source.GetStatus(token);

        /// <inheritdoc />
        void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => source.OnCompleted(continuation, state, token, flags);
    }

    /// <summary>
    /// Represents lightweight version of <see cref="Task{TResult}"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the asynchronous result.</typeparam>
    public abstract class Future<TResult> : IFuture, IValueTaskSource<TResult>
    {
         /// <summary>
        /// Represents awaiter of the asynchronous computation result represented by future object.
        /// </summary>
        public interface IAwaiter
        {
            /// <summary>
            /// Ends the wait for the completion of the asynchronous task.
            /// </summary>
            /// <returns>The result of asynchronous computation.</returns>
            TResult GetResult();
        }

        private readonly ValueTaskSourceOnCompletedFlags flags;
        private ManualResetValueTaskSourceCore<TResult> source;

        /// <summary>
        /// Initializes a new future.
        /// </summary>
        /// <param name="runContinuationsAsynchronously"><see langword="true"/> to force continuations to run asynchronously; otherwise, <see langword="false"/>.</param>
        /// <param name="continueOnCapturedContext"><see langword="true"/> to attempt to marshal the continuation back to the original context captured; otherwise, <see langword="false"/>.</param>
        protected Future(bool runContinuationsAsynchronously = true, bool continueOnCapturedContext = false)
        {
            source = new ManualResetValueTaskSourceCore<TResult>
            {
                RunContinuationsAsynchronously = runContinuationsAsynchronously,
            };

            flags = continueOnCapturedContext ? ValueTaskSourceOnCompletedFlags.UseSchedulingContext : ValueTaskSourceOnCompletedFlags.None;
        }

        /// <summary>
        /// Determines whether asynchronous operation referenced by this object is already completed.
        /// </summary>
        public bool IsCompleted => source.GetStatus(source.Version) != ValueTaskSourceStatus.Pending;

        /// <summary>
        /// Completes future unsuccessfully.
        /// </summary>
        /// <param name="e">The exception representing fault.</param>
        protected void Complete(Result<TResult> e)
        {
            if (e.IsSuccessful)
                source.SetResult(e.OrDefault()!);
            else
                source.SetException(e.Error!);
        }

        /// <summary>
        /// Attaches the callback that will be invoked on completion.
        /// </summary>
        /// <param name="callback">The callback to be attached to the asynchronous operation which result is represented by this awaitable object.</param>
        public void OnCompleted(Action callback)
            => source.OnCompleted(callback, source.Version, flags);

        /// <summary>
        /// Gets value associated with this future.
        /// </summary>
        /// <returns>The value associated with this future.</returns>
        protected TResult GetResult() => source.GetResult(source.Version);

        /// <summary>
        /// Converts this future to the task.
        /// </summary>
        /// <returns>The task representing this future.</returns>
        public ValueTask<TResult> AsTask() => new ValueTask<TResult>(this, source.Version);

        /// <inheritdoc />
        TResult IValueTaskSource<TResult>.GetResult(short token) => source.GetResult(token);

        /// <inheritdoc />
        ValueTaskSourceStatus IValueTaskSource<TResult>.GetStatus(short token) => source.GetStatus(token);

        /// <inheritdoc />
        void IValueTaskSource<TResult>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => source.OnCompleted(continuation, state, token, flags);
    }
}