using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
    internal class CancelableTaskCompletionSource<TResult> : TaskCompletionSource<TResult>, IDisposable
    {
        private CancellationTokenRegistration registration;

        internal CancelableTaskCompletionSource(ref CancellationToken token, TaskCreationOptions options = TaskCreationOptions.RunContinuationsAsynchronously)
            : base(options)
        {
            registration = token.CanBeCanceled ? token.Register(Dispose) : default;
        }

        public void Dispose()
        {
            registration.Dispose();
            registration = default;
            TrySetCanceled();
        }
    }
}