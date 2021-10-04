namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

/// <summary>
/// Represents a delegate that implements cluster node announcement logic.
/// </summary>
/// <typeparam name="TAddress">The type of the node address.</typeparam>
/// <param name="id">The identifier of the cluster member.</param>
/// <param name="address">The address of the cluster member.</param>
/// <param name="token">The token that can be used to cancel the operation.</param>
/// <returns>The task representing asynchronous result.</returns>
public delegate Task ClusterMemberAnnouncer<in TAddress>(ClusterMemberId id, TAddress address, CancellationToken token);