namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Represents configuration of cluster member.
/// </summary>
public class ClusterMemberConfiguration : IClusterMemberConfiguration
{
    private ElectionTimeout electionTimeout = ElectionTimeout.Recommended;
    private TimeSpan? rpcTimeout;

    /// <summary>
    /// Gets lower possible value of leader election timeout, in milliseconds.
    /// </summary>
    public int LowerElectionTimeout
    {
        get => electionTimeout.LowerValue;
        set => electionTimeout = electionTimeout with { LowerValue = value };
    }

    /// <summary>
    /// Gets upper possible value of leader election timeout, in milliseconds.
    /// </summary>
    public int UpperElectionTimeout
    {
        get => electionTimeout.UpperValue;
        set => electionTimeout = electionTimeout with { UpperValue = value };
    }

    /// <summary>
    /// Gets or sets Raft RPC timeout.
    /// </summary>
    public TimeSpan RpcTimeout
    {
        get => rpcTimeout ?? TimeSpan.FromMilliseconds(UpperElectionTimeout / 2D);
        set => rpcTimeout = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <inheritdoc cref="RaftCluster.NodeConfiguration.HeartbeatThreshold"/>
    public double HeartbeatThreshold
    {
        get;
        set => field = double.IsFinite(value) && value > 0D ? value : throw new ArgumentOutOfRangeException(nameof(value));
    } = 0.5D;

    /// <summary>
    /// A bound on clock drift across servers.
    /// </summary>
    /// <remarks>
    /// Over a given time period, no server’s clock increases more than this bound times any other.
    /// </remarks>
    public double ClockDriftBound
    {
        get;
        set => field = double.IsFinite(value) && value >= 1D ? value : throw new ArgumentOutOfRangeException(nameof(value));
    } = 1D;

    /// <summary>
    /// Gets or sets a value indicating that the initial node in the cluster is starting.
    /// </summary>
    public bool ColdStart { get; set; } = true;

    /// <inheritdoc/>
    ElectionTimeout IClusterMemberConfiguration.ElectionTimeout => electionTimeout;

    /// <summary>
    /// Gets metadata associated with local cluster member.
    /// </summary>
    public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

    /// <inheritdoc cref="IClusterMemberConfiguration.Standby"/>
    public bool Standby { get; set; }

    /// <summary>
    /// Gets or sets the numbers of rounds used to warm up a fresh node which wants to join the cluster.
    /// </summary>
    public int WarmupRounds
    {
        get;
        set => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    } = 10;

    /// <inheritdoc cref="IClusterMemberConfiguration.MaxReplicationLag"/>
    public int MaxReplicationLag
    {
        get;
        set => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    } = 16;

    /// <inheritdoc cref="IClusterMemberConfiguration.IsLeaderLeaseEnabled"/>
    public bool IsLeaderLeaseEnabled
    {
        get;
        set;
    }

    /// <inheritdoc cref="IClusterMemberConfiguration.AggressiveLeaderStickiness"/>
    public bool AggressiveLeaderStickiness { get; set; }
}