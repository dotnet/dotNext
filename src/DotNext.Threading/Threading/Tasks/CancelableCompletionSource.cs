using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading.Tasks
{
    internal class CancelableCompletionSource<T> : TaskCompletionSource<T>, ICancellationSupport, IDisposable
    {
        private readonly CancellationTokenRegistration registration;
        private readonly CancellationTokenSource? source;

        internal CancelableCompletionSource(TaskCreationOptions options, TimeSpan timeout, CancellationToken token)
            : base(options)
        {
            if (timeout > InfiniteTimeSpan)
            {
                source = token.CanBeCanceled ?
                    CancellationTokenSource.CreateLinkedTokenSource(token) :
                    new();
                source.CancelAfter(timeout);
                token = source.Token;
            }

            registration = ICancellationSupport.Attach(token, this);
        }

        // only for cancelable token without timeout
        internal CancelableCompletionSource(TaskCreationOptions options, CancellationToken token)
            : base(options)
            => registration = ICancellationSupport.Attach(token, this);

        void ICancellationSupport.RequestCancellation()
        {
            var token = registration.Token;
            if (!token.IsCancellationRequested)
                token = new(true);

            TrySetCanceled(token);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                registration.Dispose();
                source?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~CancelableCompletionSource() => Dispose(false);

        public static implicit operator Task<T>(CancelableCompletionSource<T> source)
            => source.Task;
    }
}