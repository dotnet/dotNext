namespace DotNext.Net.Cluster.Consensus.Raft;

using ClusterMemberIdConverter = ComponentModel.ClusterMemberIdConverter;

/// <summary>
/// Represents configuration of cluster member.
/// </summary>
public class ClusterMemberConfiguration : IClusterMemberConfiguration
{
    private ElectionTimeout electionTimeout = ElectionTimeout.Recommended;
    private TimeSpan? rpcTimeout;
    private double clockDriftBound = 1D, heartbeatThreshold = 0.5D;
    private int warmupRounds = 10;

    static ClusterMemberConfiguration() => ClusterMemberIdConverter.Register();

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

    /// <summary>
    /// Gets or sets threshold of the heartbeat timeout.
    /// </summary>
    public double HeartbeatThreshold
    {
        get => heartbeatThreshold;
        set => heartbeatThreshold = double.IsFinite(value) && value > 0D ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// A bound on clock drift across servers.
    /// </summary>
    /// <remarks>
    /// Over a given time period, no server’s clock increases more than this bound times any other.
    /// </remarks>
    public double ClockDriftBound
    {
        get => clockDriftBound;
        set => clockDriftBound = double.IsFinite(value) && value >= 1D ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets a value indicating that the initial node in the cluster is starting.
    /// </summary>
    public bool ColdStart { get; set; } = true;

    /// <inheritdoc/>
    ElectionTimeout IClusterMemberConfiguration.ElectionTimeout => electionTimeout;

    /// <summary>
    /// Indicates that each part of cluster in partitioned network allow to elect its own leader.
    /// </summary>
    /// <remarks>
    /// <see langword="false"/> value allows to build CA distributed cluster
    /// while <see langword="true"/> value allows to build CP/AP distributed cluster.
    /// </remarks>
    public bool Partitioning { get; set; }

    /// <summary>
    /// Gets metadata associated with local cluster member.
    /// </summary>
    public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets a value indicating that the cluster member
    /// represents standby node which is never become a leader.
    /// </summary>
    public bool Standby { get; set; }

    /// <summary>
    /// Gets or sets the numbers of rounds used to warmup a fresh node which wants to join the cluster.
    /// </summary>
    public int WarmupRounds
    {
        get => warmupRounds;
        set => warmupRounds = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(warmupRounds));
    }

    /// <summary>
    /// Gets a value indicating that the follower node should not try to upgrade
    /// to the candidate state if the leader is reachable via the network.
    /// </summary>
    public bool AggressiveLeaderStickiness { get; set; }

    /// <summary>
    /// Gets or sets custom member identifier. If not set, it will be generated randomly.
    /// </summary>
    public ClusterMemberId? Id { get; set; }
}