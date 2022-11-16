using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Diagnostics;

public partial class PhiAccrualFailureDetector
{
    /// <summary>
    /// Represents snapshot of the heartbeat history.
    /// </summary>
    private class HeartbeatSlidingWindow
    {
        private protected readonly Timestamp currentTs;
        internal readonly int MaxSampleSize;

        private protected HeartbeatSlidingWindow(Timestamp ts, int maxSampleSize)
        {
            currentTs = ts;
            MaxSampleSize = maxSampleSize;
        }

        private HeartbeatSlidingWindow(int maxSampleSize) => MaxSampleSize = maxSampleSize;

        internal virtual double Phi(Timestamp ts, TimeSpan acceptableHeartbeatPause, TimeSpan minStdDeviation, out TimeSpan interval)
        {
            interval = default;
            return 0.0D;
        }

        internal virtual HeartbeatSlidingWindow Next(Timestamp stamp)
            => currentTs.IsEmpty ? new HeartbeatSlidingWindow(stamp, MaxSampleSize) : new HeartbeatHistorySnapshot(stamp, MaxSampleSize, stamp.Value - currentTs.Value);

        internal static HeartbeatSlidingWindow Initial(int maxSampleSize) => new(maxSampleSize);
    }

    private sealed class HeartbeatHistorySnapshot : HeartbeatSlidingWindow
    {
        private readonly TimeSpan intervalSum, squaredIntervalSum;
        private readonly HeartbeatHistorySnapshot? previous;
        private readonly TimeSpan value;
        private readonly TimeSpan[]? backlog;
        private readonly int count, remainingOffset;

        internal HeartbeatHistorySnapshot(Timestamp ts, int maxSampleSize, TimeSpan interval)
            : base(ts, maxSampleSize)
        {
            value = interval;
            count = 1;
            intervalSum = interval;
            squaredIntervalSum = Pow2(interval);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private HeartbeatHistorySnapshot(Timestamp ts, HeartbeatHistorySnapshot previous)
            : base(ts, previous.MaxSampleSize)
        {
            value = ts.Value - previous.currentTs.Value;
            intervalSum = previous.intervalSum;
            squaredIntervalSum = previous.squaredIntervalSum;

            if (previous.count >= MaxSampleSize)
            {
                TimeSpan oldValue;
                count = previous.count;
                if (previous.NewBacklogRequired)
                {
                    backlog = previous.GetBacklog(out oldValue);
                }
                else
                {
                    backlog = previous.backlog;
                    oldValue = backlog[remainingOffset = previous.remainingOffset + 1];
                    this.previous = previous;
                }

                intervalSum -= oldValue;
                squaredIntervalSum -= Pow2(oldValue);
            }
            else
            {
                count = previous.count + 1;
                this.previous = previous;
            }

            intervalSum += value;
            squaredIntervalSum += Pow2(value);
        }

        private TimeSpan[] GetBacklog(out TimeSpan removedItem)
        {
            var result = GC.AllocateUninitializedArray<TimeSpan>(count - 1);

            var current = this;
            for (var index = result.Length - 1; current is not null && index >= 0; index--, current = current.previous)
                result[index] = current.value!;

            Debug.Assert(current is not null);
            removedItem = current.value!;

            return result;
        }

        [MemberNotNullWhen(false, nameof(backlog))]
        private bool NewBacklogRequired => backlog is null || remainingOffset == backlog.Length;

        private static TimeSpan Pow2(TimeSpan x)
        {
            var ms = x.TotalMilliseconds;
            return TimeSpan.FromMilliseconds(ms * ms);
        }

        private TimeSpan Mean => intervalSum / count;

        private TimeSpan Variance => (squaredIntervalSum / count) - Pow2(Mean);

        private double StdDeviation => Math.Sqrt(Variance.TotalMilliseconds);

        internal override HeartbeatHistorySnapshot Next(Timestamp ts) => new(ts, this);

        internal override double Phi(Timestamp ts, TimeSpan acceptableHeartbeatPause, TimeSpan minStdDeviation, out TimeSpan interval)
        {
            interval = ts.Value - currentTs.Value;
            var intervalMillis = interval.TotalMilliseconds;
            var meanMillis = (Mean + acceptableHeartbeatPause).TotalMilliseconds;
            var deviationMillis = Math.Max(StdDeviation, minStdDeviation.TotalMilliseconds);

            var y = (intervalMillis - meanMillis) / deviationMillis;
            var e = Math.Exp(-y * (1.5976 + (0.070566 * y * y)));

            return -Math.Log10(intervalMillis > meanMillis ? e / (1D + e) : 1D - (1D / (1D + e)));
        }
    }
}