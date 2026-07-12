using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading.Tasks;

partial class ManualResetCompletionSource
{
    private Action<object?, CancellationToken>? cancellationCallback;
    private TimerCallback? timeoutCallback;
    
    private short Activate<TActivator>(TActivator activator)
        where TActivator : struct, IActivator, allows ref struct
    {
        if (BeginActivation(out var version))
        {
            try
            {
                activator.Activate(this, version);
            }
            finally
            {
                EndActivation();
            }
        }

        return version;
    }
    
    private void EnableCancellation(short version, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            CancellationRequested(new CancellationReason(version, token));
        }
        else
        {
            cancellationCallback ??= CancellationRequested;
            CachedVersion = version;
            tokenTracker = token.UnsafeRegister(cancellationCallback, cachedVersion);
        }
    }

    private void EnableTimeout(short version, TimeSpan timeout)
    {
        timeoutCallback ??= TimeoutOccurred;
        CachedVersion = version;

        Arm(ref timeoutTracker, timeoutCallback, cachedVersion, timeout);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void CancellationRequested(object? expectedVersion, CancellationToken token)
    {
        Debug.Assert(expectedVersion is short);

        CancellationRequested(new CancellationReason((short)expectedVersion, token));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TimeoutOccurred(object? expectedVersion)
    {
        Debug.Assert(expectedVersion is short);

        CancellationRequested(new TimeoutReason((short)expectedVersion));
    }
    
    private short CachedVersion
    {
        [MemberNotNull(nameof(cachedVersion))]
        set
        {
            if (cachedVersion is null)
            {
                cachedVersion = value;
            }
            else
            {
                Unsafe.Unbox<short>(cachedVersion) = value;
            }
        }
    }
    
    private interface IActivator
    {
        void Activate(ManualResetCompletionSource source, short version);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct TimeoutAndCancellationTokenActivation(TimeSpan timeout, CancellationToken token) : IActivator
    {
        void IActivator.Activate(ManualResetCompletionSource source, short version)
        {
            // Do not change the order of the method calls below. Otherwise, cancellation callback
            // can be triggered concurrently with the timer initialization.
            source.EnableTimeout(version, timeout);
            source.EnableCancellation(version, token);
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct TimeoutActivation(TimeSpan timeout) : IActivator
    {
        void IActivator.Activate(ManualResetCompletionSource source, short version)
            => source.EnableTimeout(version, timeout);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct CancellationTokenActivation(CancellationToken token) : IActivator
    {
        void IActivator.Activate(ManualResetCompletionSource source, short version)
            => source.EnableCancellation(version, token);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct TimedOutActivation : IActivator
    {
        void IActivator.Activate(ManualResetCompletionSource source, short version)
            => source.CancellationRequested(new TimeoutReason(version));
    }
    
    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct NoOpActivation : IActivator
    {
        void IActivator.Activate(ManualResetCompletionSource source, short version)
        {
            // nothing to do
        }
    }
}