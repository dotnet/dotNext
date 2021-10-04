namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

/// <summary>
/// Provides a storage of cluster members.
/// </summary>
public interface IClusterConfigurationStorage : IDisposable
{
    /// <summary>
    /// Represents active cluster configuration maintained by the node.
    /// </summary>
    IClusterConfiguration ActiveConfiguration { get; }

    /// <summary>
    /// Represents proposed cluster configuration.
    /// </summary>
    IClusterConfiguration? ProposedConfiguration { get; }

    /// <summary>
    /// Loads configuration from the storage.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    ValueTask LoadConfigurationAsync(CancellationToken token);

    /// <summary>
    /// Proposes the configuration.
    /// </summary>
    /// <remarks>
    /// If method is called multiple times then <see cref="ProposedConfiguration"/> will be rewritten.
    /// </remarks>
    /// <param name="configuration">The proposed configuration.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    ValueTask ProposeAsync(IClusterConfiguration configuration, CancellationToken token = default);

    /// <summary>
    /// Applies proposed configuration as active configuration.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    ValueTask ApplyAsync(CancellationToken token = default);

    /// <summary>
    /// Waits until the proposed configuration becomes active.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    Task WaitForApplyAsync(CancellationToken token = default);
}

/// <summary>
/// Provides a storage of cluster members.
/// </summary>
/// <typeparam name="TAddress">The type of the cluster member address.</typeparam>
public interface IClusterConfigurationStorage<TAddress> : IClusterConfigurationStorage
    where TAddress : notnull
{
    /// <summary>
    /// Proposes a new member.
    /// </summary>
    /// <param name="id">The identifier of the cluster member to add.</param>
    /// <param name="address">The address of the cluster member.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the new member is added to the proposed configuration;
    /// <see langword="false"/> if the storage has the proposed configuration already.
    /// </returns>
    ValueTask<bool> AddMemberAsync(ClusterMemberId id, TAddress address, CancellationToken token = default);

    /// <summary>
    /// Proposes removal of the existing member.
    /// </summary>
    /// <param name="id">The identifier of the cluster member to remove.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the new member is added to the proposed configuration;
    /// <see langword="false"/> if the storage has the proposed configuration already.
    /// </returns>
    ValueTask<bool> RemoveMemberAsync(ClusterMemberId id, CancellationToken token = default);

    /// <summary>
    /// Polls for change in cluster configuration.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The stream of configuration events.</returns>
    IAsyncEnumerable<ClusterConfigurationEvent<TAddress>> PollChangesAsync(CancellationToken token = default);

    /// <summary>
    /// Represents active cluster configuration maintained by the node.
    /// </summary>
    new IReadOnlyDictionary<ClusterMemberId, TAddress> ActiveConfiguration { get; }

    /// <summary>
    /// Represents proposed cluster configuration.
    /// </summary>
    new IReadOnlyDictionary<ClusterMemberId, TAddress>? ProposedConfiguration { get; }
}