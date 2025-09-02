using System.Runtime.InteropServices;

namespace DotNext.Threading;

partial class QueuedSynchronizer
{
    /// <summary>
    /// Represents acquisition options.
    /// </summary>
    private protected interface IAcquisitionOptions
    {
        CancellationToken Token { get; }

        TimeSpan Timeout { get; }

        object? InterruptionReason => null;

        static virtual bool InterruptionRequired => false;
    }

    private protected interface IAcquisitionOptionsWithTimeout : IAcquisitionOptions;

    [StructLayout(LayoutKind.Auto)]
    private protected readonly struct CancellationTokenOnly : IAcquisitionOptions
    {
        internal CancellationTokenOnly(CancellationToken token) => Token = token;

        public CancellationToken Token { get; }

        TimeSpan IAcquisitionOptions.Timeout => new(Timeout.InfiniteTicks);
    }

    [StructLayout(LayoutKind.Auto)]
    private protected readonly struct TimeoutAndCancellationToken : IAcquisitionOptionsWithTimeout
    {
        internal TimeoutAndCancellationToken(TimeSpan timeout, CancellationToken token)
        {
            Timeout = timeout;
            Token = token;
        }

        public CancellationToken Token { get; }

        public TimeSpan Timeout { get; }
    }

    [StructLayout(LayoutKind.Auto)]
    private protected readonly struct InterruptionReasonAndCancellationToken : IAcquisitionOptions
    {
        internal InterruptionReasonAndCancellationToken(object? reason, CancellationToken token)
        {
            InterruptionReason = reason;
            Token = token;
        }

        public CancellationToken Token { get; }

        public object? InterruptionReason { get; }

        static bool IAcquisitionOptions.InterruptionRequired => true;

        TimeSpan IAcquisitionOptions.Timeout => new(Timeout.InfiniteTicks);
    }

    [StructLayout(LayoutKind.Auto)]
    private protected readonly struct TimeoutAndInterruptionReasonAndCancellationToken : IAcquisitionOptionsWithTimeout
    {
        internal TimeoutAndInterruptionReasonAndCancellationToken(object? reason, TimeSpan timeout, CancellationToken token)
        {
            InterruptionReason = reason;
            Timeout = timeout;
            Token = token;
        }

        public CancellationToken Token { get; }

        public object? InterruptionReason { get; }

        static bool IAcquisitionOptions.InterruptionRequired => true;

        public TimeSpan Timeout { get; }
    }
}