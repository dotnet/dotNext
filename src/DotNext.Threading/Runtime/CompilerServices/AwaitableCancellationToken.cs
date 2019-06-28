using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Represents cancellation token turned into awaitable object.
    /// </summary>
    public sealed class AwaitableCancellationToken : Awaitable<Task>
    {
        private static readonly object CanceledToken = new CancellationToken(true);
        internal static readonly AwaitableCancellationToken Completed = new AwaitableCancellationToken(false);
        internal static readonly AwaitableCancellationToken Canceled = new AwaitableCancellationToken(true);

        /// <summary>
        /// Represents object that is used to monitor the completion of an asynchronous operation
        /// </summary>
        public readonly struct Awaiter : INotifyCompletion
        {
            private readonly AwaitableCancellationToken awaitable;

            internal Awaiter(AwaitableCancellationToken awaitable) => this.awaitable = awaitable;

            /// <summary>
            /// Indicates that underlying token is canceled.
            /// </summary>
            public bool IsCompleted => awaitable is null || awaitable.IsCompleted;

            /// <summary>
            /// Checks whether the underlying token is canceled.
            /// </summary>
            /// <exception cref="OperationCanceledException">Cancellation requested and caller specified that exception should be thrown.</exception>
            public void GetResult()
            {
                if (awaitable is null || !awaitable.throwIfCanceled)
                    return;
                if (awaitable.state is CancellationToken token)
                    token.ThrowIfCancellationRequested();
                throw new InvalidOperationException();
            }

            /// <summary>
            /// Sets the continuation to invoke.
            /// </summary>
            /// <param name="continuation">The action to invoke asynchronously.</param>
            public void OnCompleted(Action continuation)
            {
                if (IsCompleted)
                    continuation();
                else
                    awaitable.OnCompleted(continuation);
            }
        }

        private object state;
        private readonly CancellationTokenRegistration registration;
        private readonly bool throwIfCanceled;

        internal AwaitableCancellationToken(bool throwIfCanceled, ref CancellationToken token)
        {
            this.throwIfCanceled = throwIfCanceled;
            if (token.IsCancellationRequested)
                state = CanceledToken;
            else
                registration = token.Register(OnCanceled, token);
        }

        private AwaitableCancellationToken(bool throwIfCanceled)
        {
            this.throwIfCanceled = throwIfCanceled;
            state = CanceledToken;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void OnCanceled(object token)
        {
            state = token;
            registration.Dispose();
            continuation?.Invoke();
            continuation = null;
        }

        /// <summary>
        /// Indicates that underlying token is canceled.
        /// </summary>
        public override bool IsCompleted => state != null;

        /// <summary>
        /// Retrieves awaiter for underlying wait handle.
        /// </summary>
        /// <returns>The object that is used to monitor the completion of an asynchronous operation.</returns>
        public Awaiter GetAwaiter() => new Awaiter(this);

        /// <summary>
        /// Converts cancellation token into <see cref="Task"/>.
        /// </summary>
        /// <returns>The task representing cancellation token.</returns>
        public override async Task AsTask() => await this;
    }
}
