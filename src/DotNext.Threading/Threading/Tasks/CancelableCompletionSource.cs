using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading.Tasks
{
    internal class CancelableCompletionSource<T> : TaskCompletionSource<T>, IDisposable
    {
        // cached callback to avoid extra memory allocation
        private static readonly Action<object> CancellationCallback;

        private readonly CancellationTokenRegistration registration;
        private readonly CancellationTokenSource? source;

        static CancelableCompletionSource()
        {
            CancellationCallback = CancellationRequested;

            static void CancellationRequested(object state)
                => Unsafe.As<CancelableCompletionSource<T>>(state).CancellationRequested();
        }

        internal CancelableCompletionSource(TaskCreationOptions options, TimeSpan timeout, CancellationToken token)
            : base(options)
        {
            if (timeout > InfiniteTimeSpan)
            {
                source = token.CanBeCanceled ?
                    CancellationTokenSource.CreateLinkedTokenSource(token) :
                    new CancellationTokenSource();
                source.CancelAfter(timeout);
                token = source.Token;
            }

            registration = token.Register(CancellationCallback, this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CancellationRequested()
        {
            var token = registration.Token;
            if (!token.IsCancellationRequested)
                token = new CancellationToken(true);

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
    }
}