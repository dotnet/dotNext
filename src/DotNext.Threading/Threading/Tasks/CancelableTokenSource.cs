using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
    /// <summary>
    /// Represents cancelable producer of <see cref="Task{TResult}"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the result value associated with the task.</typeparam>
    internal class CancelableTaskCompletionSource<TResult> : TaskCompletionSource<TResult>, IDisposable
    {
        private CancellationTokenRegistration registration;

        /// <summary>
        /// Initializes a new cancelable task producer.
        /// </summary>
        /// <param name="token">The token that can be used to cancel <see cref="TaskCompletionSource{TResult}.Task"/>.</param>
        /// <param name="options">The task options.</param>
        internal CancelableTaskCompletionSource(ref CancellationToken token, TaskCreationOptions options = TaskCreationOptions.RunContinuationsAsynchronously)
            : base(options)
        {
            registration = token.CanBeCanceled ? token.Register(Cancel, token) : default;
        }

        private void Cancel(object token)
        {
            if (token is CancellationToken unboxedToken)
                TrySetCanceled(unboxedToken);
            else
                TrySetCanceled();
        }

        /// <summary>
        /// Cancels the task and token state tracking.
        /// </summary>
        public void Dispose()
        {
            registration.Dispose();
            registration = default;
        }
    }
}