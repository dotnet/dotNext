using System;
using System.Diagnostics;
using System.Threading;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading.Tasks
{
    internal interface ICancellationSupport
    {
        // cached callback to avoid extra memory allocation
        private static readonly Action<object?> CancellationCallback = CancellationRequested;

        private static void CancellationRequested(object? state)
        {
            Debug.Assert(state is ICancellationSupport);
            Unsafe.As<ICancellationSupport>(state).RequestCancellation();
        }

        private protected void RequestCancellation();

        private protected static CancellationTokenRegistration Attach(ICancellationSupport cancellation, CancellationToken token)
        {
#if NETSTANDARD2_1
            return token.Register(CancellationCallback, cancellation);
#else
            return token.UnsafeRegister(CancellationCallback, cancellation);
#endif
        }
    }
}