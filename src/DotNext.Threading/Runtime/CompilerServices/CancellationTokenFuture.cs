using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Runtime.CompilerServices
{
    using Threading.Tasks;

    /// <summary>
    /// Represents cancellation token turned into awaitable future.
    /// </summary>
    internal sealed class CancellationTokenFuture : Future, Future.IAwaiter
    {
        // cache delegates to avoid allocations
        private static readonly Action<object?> CancellationCallback = Cancel!;
        private static readonly Action<object?> CompletionCallback = Complete!;

        private readonly CancellationTokenRegistration registration;

        internal CancellationTokenFuture(bool throwIfCanceled, CancellationToken token)
        {
            registration = token.Register(throwIfCanceled ? CancellationCallback : CompletionCallback, this);
        }

        private static void Complete(object state)
            => Unsafe.As<CancellationTokenFuture>(state).Complete();

        private static void Cancel(object state)
            => Unsafe.As<CancellationTokenFuture>(state).Cancel();

        private void Complete()
        {
            registration.Dispose();
            base.Complete(null);
        }

        private void Cancel()
        {
            var token = registration.Token;
            registration.Dispose();
            base.Complete(new OperationCanceledException(token));
        }

        /// <summary>
        /// Retrieves awaiter for underlying wait handle.
        /// </summary>
        /// <returns>The object that is used to monitor the completion of an asynchronous operation.</returns>
        public IAwaiter GetAwaiter() => this;

        void IAwaiter.GetResult() => GetResult();
    }
}
