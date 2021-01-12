using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Represents cancellation token turned into awaitable future.
    /// </summary>
    public sealed class CancellationTokenFuture : Threading.Tasks.Future<Task>, Threading.Tasks.Future.IAwaiter
    {
        private static readonly object CanceledToken = new CancellationToken(true);
        internal static readonly CancellationTokenFuture Completed = new CancellationTokenFuture(false);
        internal static readonly CancellationTokenFuture Canceled = new CancellationTokenFuture(true);

        private readonly CancellationTokenRegistration registration;
        private readonly bool throwIfCanceled;
        private object? state;

        internal CancellationTokenFuture(bool throwIfCanceled, ref CancellationToken token)
        {
            this.throwIfCanceled = throwIfCanceled;
            if (token.IsCancellationRequested)
                state = CanceledToken;
            else
                registration = token.Register(Complete);
        }

        private CancellationTokenFuture(bool throwIfCanceled)
        {
            this.throwIfCanceled = throwIfCanceled;
            state = CanceledToken;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private new void Complete()
        {
            state = registration.Token;
            registration.Dispose();
            base.Complete();
        }

        /// <summary>
        /// Indicates that underlying token is canceled.
        /// </summary>
        public override bool IsCompleted => state != null;

        /// <summary>
        /// Retrieves awaiter for underlying wait handle.
        /// </summary>
        /// <returns>The object that is used to monitor the completion of an asynchronous operation.</returns>
        public IAwaiter GetAwaiter() => this;

        /// <inheritdoc/>
        void IAwaiter.GetResult()
        {
            if (!throwIfCanceled)
                return;
            if (state is CancellationToken token)
                token.ThrowIfCancellationRequested();
            throw new IncompletedFutureException();
        }

        private async Task ExecuteAsync() => await this;

        /// <summary>
        /// Converts cancellation token into <see cref="Task"/>.
        /// </summary>
        /// <returns>The task representing cancellation token.</returns>
        public override Task AsTask()
        {
            if (state is CancellationToken token)
                return throwIfCanceled ? Task.FromCanceled(token) : Task.CompletedTask;
            else
                return ExecuteAsync();
        }
    }
}
