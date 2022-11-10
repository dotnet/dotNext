using static System.Globalization.CultureInfo;

namespace DotNext.Diagnostics;

/// <summary>
/// Represents Phi Accrual implementation of the failure detector.
/// </summary>
/// <seealso href="https://oneofus.la/have-emacs-will-hack/files/HDY04.pdf">The Phi Accrual Failure Detector</seealso>
/// <seealso href="https://github.com/akka/akka/blob/main/akka-remote/src/main/scala/akka/remote/PhiAccrualFailureDetector.scala">Implementation in Scala</seealso>
public partial class PhiAccrualFailureDetector : IFailureDetector, ISupplier<double>
{
    private readonly double threshold = 16.0;
    private readonly TimeSpan minStdDeviation = TimeSpan.FromMilliseconds(500D),
        acceptableHeartbeatPause = TimeSpan.Zero,
        suspiciousGrowthThreshold = TimeSpan.Zero;

    private volatile HeartbeatHistorySnapshot snapshot = HeartbeatHistorySnapshot.Initial(200);
    private Action<TimeSpan>? suspiciousGrowthCallback;

    /// <summary>
    /// Gets or sets Phi value threshold that is used to determine whether the underlying resource is no longer available.
    /// </summary>
    /// <remarks>
    /// A low threshold is prone to generate many wrong suspicions but ensures a quick detection in the event
    /// of a real crash. Conversely, a high threshold generates fewer mistakes but needs more time to detect
    /// actual crashes.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is less than or equal to 0.</exception>
    public double Threshold
    {
        get => threshold;
        init => threshold = value > 0D ? value : throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or sets a number of samples to use for calculation of mean and standard deviation of inter-arrival times.
    /// </summary>
    public int MaxSampleSize
    {
        get => snapshot.MaxSampleSize;
        init => snapshot = value > 0 ? HeartbeatHistorySnapshot.Initial(value) : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets minimum standard deviation to use for the normal distribution used when calculating phi.
    /// </summary>
    /// <remarks>
    /// Too low standard deviation might result in too much sensitivity for sudden, but normal, deviations
    /// in heartbeat inter-arrival times.
    /// </remarks>
    public TimeSpan MinStdDeviation
    {
        get => minStdDeviation;
        init => minStdDeviation = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets duration corresponding to number of potentially lost/delayed
    /// heartbeats that will be accepted before considering it to be an anomaly.
    /// </summary>
    /// <remarks>
    /// Too low standard deviation might result in too much sensitivity for sudden, but normal, deviations
    /// in heartbeat inter arrival times.
    /// </remarks>
    public TimeSpan AcceptableHeartbeatPause
    {
        get => acceptableHeartbeatPause;
        init
        {
            acceptableHeartbeatPause = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
            suspiciousGrowthThreshold = value / 3L * 2L;
        }
    }

    /// <summary>
    /// Gets Phi value.
    /// </summary>
    public double Value => GetValue(new());

    /// <summary>
    /// Gets Phi value.
    /// </summary>
    /// <param name="ts">The timestamp of the heartbeat.</param>
    /// <returns>Phi value.</returns>
    public double GetValue(Timestamp ts) => snapshot.Phi(ts, acceptableHeartbeatPause, minStdDeviation, out _);

    /// <inheritdoc />
    double ISupplier<double>.Invoke() => Value;

    /// <summary>
    /// Notifies that this detector received a heartbeat from the associated resource.
    /// </summary>
    public void ReportHeartbeat() => ReportHeartbeat(new());

    /// <summary>
    /// Indicates that the resource associated with this detector is considered to be up
    /// and healthy.
    /// </summary>
    public bool IsHealthy => Value < threshold;

    /// <summary>
    /// Indicates that this detector has received any heartbeats and started monitoring of the resource.
    /// </summary>
    public bool IsMonitoring => snapshot.GetType() != typeof(HeartbeatHistorySnapshot);

    /// <summary>
    /// Notifies that this detector received a heartbeat from the associated resource.
    /// </summary>
    /// <param name="ts">The timestamp of the heartbeat.</param>
    public void ReportHeartbeat(Timestamp ts)
    {
        TimeSpan interval;
        HeartbeatHistorySnapshot currentSnapshot, newSnapshot = snapshot;

        do
        {
            currentSnapshot = newSnapshot;

            if (currentSnapshot.Phi(ts, acceptableHeartbeatPause, minStdDeviation, out interval) < threshold)
                newSnapshot = currentSnapshot.Next(ts);
        }
        while (!ReferenceEquals(newSnapshot = Interlocked.CompareExchange(ref snapshot, newSnapshot, currentSnapshot), currentSnapshot));

        if (interval >= suspiciousGrowthThreshold)
            suspiciousGrowthCallback?.Invoke(interval);
    }

    /// <summary>
    /// An event occurred when this detector considers than the interval between two heartbeats is growing too fast.
    /// </summary>
    public event Action<TimeSpan> SuspiciousIntervalGrowth
    {
        add => suspiciousGrowthCallback += value;
        remove => suspiciousGrowthCallback -= value;
    }

    /// <summary>
    /// Resets this detector to its initial state.
    /// </summary>
    public void Reset()
    {
        HeartbeatHistorySnapshot currentSnapshot, newSnapshot = snapshot;

        do
        {
            currentSnapshot = newSnapshot;

            newSnapshot = HeartbeatHistorySnapshot.Initial(currentSnapshot.MaxSampleSize);
        }
        while (!ReferenceEquals(newSnapshot = Interlocked.CompareExchange(ref snapshot, newSnapshot, currentSnapshot), currentSnapshot));
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(InvariantCulture);
}