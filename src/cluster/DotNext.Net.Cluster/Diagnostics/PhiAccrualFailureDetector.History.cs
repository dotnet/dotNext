using System.Collections.Immutable;

namespace DotNext.Diagnostics;

public partial class PhiAccrualFailureDetector
{
    /// <summary>
    /// Represents snapshot of the heartbeat history.
    /// </summary>
    private class HeartbeatHistorySnapshot
    {
        private protected readonly Timestamp currentTs;
        internal readonly int MaxSampleSize;

        private protected HeartbeatHistorySnapshot(Timestamp ts, int maxSampleSize)
        {
            currentTs = ts;
            MaxSampleSize = maxSampleSize;
        }

        private HeartbeatHistorySnapshot(int maxSampleSize) => MaxSampleSize = maxSampleSize;

        internal virtual double Phi(Timestamp ts, TimeSpan acceptableHeartbeatPause, TimeSpan minStdDeviation, out TimeSpan interval)
        {
            interval = default;
            return 0.0D;
        }

        internal virtual HeartbeatHistorySnapshot Next(Timestamp stamp)
            => currentTs.IsEmpty ? new HeartbeatHistorySnapshot(stamp, MaxSampleSize) : new HeartbeatHistorySnapshotData(stamp, MaxSampleSize, (stamp - currentTs).Value);

        internal static HeartbeatHistorySnapshot Initial(int maxSampleSize) => new(maxSampleSize);
    }

    private sealed class HeartbeatHistorySnapshotData : HeartbeatHistorySnapshot
    {
        private readonly TimeSpan intervalSum, squaredIntervalSum;
        private readonly ImmutableQueue<TimeSpan> intervals;
        private readonly long size;

        internal HeartbeatHistorySnapshotData(Timestamp ts, int maxSampleSize, TimeSpan interval)
            : base(ts, maxSampleSize)
        {
            intervals = ImmutableQueue.Create(interval);
            size = 1L;
            intervalSum = interval;
            squaredIntervalSum = Pow2(interval);
        }

        private HeartbeatHistorySnapshotData(Timestamp ts, HeartbeatHistorySnapshotData previous)
            : base(ts, previous.MaxSampleSize)
        {
            intervals = previous.intervals;
            intervalSum = previous.intervalSum;
            squaredIntervalSum = previous.squaredIntervalSum;

            TimeSpan interval;
            if (previous.size >= MaxSampleSize)
            {
                intervals = intervals.Dequeue(out interval);
                intervalSum -= interval;
                squaredIntervalSum -= Pow2(interval);
                size = previous.size;
            }
            else
            {
                size = previous.size + 1L;
            }

            interval = (ts - previous.currentTs).Value;
            intervals = intervals.Enqueue(interval);
            intervalSum += interval;
            squaredIntervalSum += Pow2(interval);
        }

        private static TimeSpan Pow2(TimeSpan x)
        {
            var ms = x.TotalMilliseconds;
            return TimeSpan.FromMilliseconds(ms * ms);
        }

        private TimeSpan Mean => intervalSum / size;

        private TimeSpan Variance => (squaredIntervalSum / size) - Pow2(Mean);

        private double StdDeviation => Math.Sqrt(Variance.TotalMilliseconds);

        internal override HeartbeatHistorySnapshotData Next(Timestamp ts) => new(ts, this);

        internal override double Phi(Timestamp ts, TimeSpan acceptableHeartbeatPause, TimeSpan minStdDeviation, out TimeSpan interval)
        {
            interval = (ts - currentTs).Value;
            var intervalMillis = (ts - currentTs).Value.TotalMilliseconds;
            var meanMillis = (Mean + acceptableHeartbeatPause).TotalMilliseconds;
            var deviationMillis = Math.Max(StdDeviation, minStdDeviation.TotalMilliseconds);

            var y = (intervalMillis - meanMillis) / deviationMillis;
            var e = Math.Exp(-y * (1.5976 + (0.070566 * y * y)));

            return -Math.Log10(intervalMillis > meanMillis ? e / (1D + e) : 1D - (1D / (1D + e)));
        }
    }
}