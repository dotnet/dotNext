using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Diagnostics;

public partial class PhiAccrualFailureDetector
{
    private class Measurement
    {
        internal readonly double Interval;
        internal readonly int MaxSampleSize;
        private readonly double intervalSum, squaredIntervalSum;
        private readonly Measurement? previous;
        private readonly double[]? backlog;
        private readonly int count, remainingOffset;

        internal Measurement(int maxSampleSize, double interval)
        {
            MaxSampleSize = maxSampleSize;
            Interval = interval;
            count = 1;
            intervalSum = interval;
            squaredIntervalSum = Squared(interval);
        }

        protected Measurement(Measurement origin)
        {
            Debug.Assert(origin is not null);

            MaxSampleSize = origin.MaxSampleSize;
            Interval = origin.Interval;
            count = origin.count;
            remainingOffset = origin.remainingOffset;
            intervalSum = origin.intervalSum;
            squaredIntervalSum = origin.squaredIntervalSum;
            previous = origin.previous;
            backlog = origin.backlog;
        }

        private Measurement(Measurement origin, int maxSampleSize)
            : this(origin)
            => MaxSampleSize = maxSampleSize;

        protected Measurement(Measurement previous, double interval)
        {
            Debug.Assert(previous is not null);

            MaxSampleSize = previous.MaxSampleSize;
            Interval = interval;
            intervalSum = previous.intervalSum;
            squaredIntervalSum = previous.squaredIntervalSum;

            if (previous.count >= MaxSampleSize)
            {
                double oldValue;
                count = previous.count;
                if (previous.NewBacklogRequired)
                {
                    backlog = previous.GetBacklog(out oldValue);
                }
                else
                {
                    backlog = previous.backlog;
                    oldValue = backlog[previous.remainingOffset];
                    remainingOffset = previous.remainingOffset + 1;
                    this.previous = previous;
                }

                intervalSum -= oldValue;
                squaredIntervalSum -= Squared(oldValue);
            }
            else
            {
                count = previous.count + 1;
                this.previous = previous;
            }

            intervalSum += Interval;
            squaredIntervalSum += Squared(Interval);
        }

        private double[] GetBacklog(out double removedItem)
        {
            var result = GC.AllocateUninitializedArray<double>(MaxSampleSize - 1);

            var current = this;
            for (var index = result.Length - 1; current is not null && index >= 0; index--, current = current.previous)
                result[index] = current.Interval;

            Debug.Assert(current is not null);
            removedItem = current.Interval;

            return result;
        }

        internal Measurement SetMaxSampleSize(int value) => new(this, value);

        [MemberNotNullWhen(false, nameof(backlog))]
        private bool NewBacklogRequired => backlog is null || remainingOffset == backlog.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Squared(double x) => x * x;

        private double Mean => intervalSum / count;

        private double Variance => (squaredIntervalSum / count) - Squared(Mean);

        private double StdDeviation => Math.Sqrt(Variance);

        internal virtual Measurement Next(Timestamp ts) => new StampedMeasurement(this, ts);

        protected double Phi(double timeSinceLastMeasurement, double acceptableHeartbeatPause, double minStdDeviation)
        {
            var meanMillis = Mean + acceptableHeartbeatPause;
            var deviationMillis = Math.Max(StdDeviation, minStdDeviation);

            Debug.Assert(double.IsNaN(deviationMillis) is false);

            var y = (timeSinceLastMeasurement - meanMillis) / deviationMillis;
            var e = Math.Exp(-y * (1.5976D + (0.070566D * Squared(y))));

            return -Math.Log10(timeSinceLastMeasurement > meanMillis ? e / (1D + e) : 1D - (1D / (1D + e)));
        }

        internal virtual double Phi(Timestamp ts, double acceptableHeartbeatPause, double minStdDeviation)
            => 0D;

        public static Measurement operator +(Measurement previous, double interval)
            => new(previous, interval);
    }

    private sealed class StampedMeasurement : Measurement
    {
        private readonly Timestamp currentTs;

        internal StampedMeasurement(Measurement origin, Timestamp ts)
            : base(origin)
            => currentTs = ts;

        private StampedMeasurement(StampedMeasurement previous, Timestamp ts)
            : base(previous, ts.ElapsedSince(previous.currentTs))
            => currentTs = ts;

        internal override StampedMeasurement Next(Timestamp ts) => new(this, ts);

        internal override double Phi(Timestamp ts, double acceptableHeartbeatPause, double minStdDeviation)
            => Phi(ts.ElapsedSince(currentTs), acceptableHeartbeatPause, minStdDeviation);
    }
}