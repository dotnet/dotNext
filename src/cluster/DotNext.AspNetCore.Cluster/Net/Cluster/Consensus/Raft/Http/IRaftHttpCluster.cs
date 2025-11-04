namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using Extensions;
using Messaging;

/// <summary>
/// Represents local view of Raft cluster built on top of ASP.NET Core infrastructure.
/// </summary>
public interface IRaftHttpCluster : IRaftCluster, IMessageBus, IStandbyModeSupport, IUnresponsiveClusterMemberRemovalSupport
{
    /// <summary>
    /// Announces a new member in the cluster.
    /// </summary>
    /// <param name="address">The address of the cluster member.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the node has been added to the cluster successfully;
    /// <see langword="false"/> if the node rejects the replication or the address of the node cannot be committed.
    /// </returns>
    /// <exception cref="NotLeaderException">The current node is not a leader.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled or the cluster elects a new leader.</exception>
    Task<bool> AddMemberAsync(Uri address, CancellationToken token = default);

    /// <summary>
    /// Removes the member from the cluster.
    /// </summary>
    /// <param name="address">The address of the cluster member.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the node has been removed from the cluster successfully;
    /// <see langword="false"/> if the node rejects the replication or the address of the node cannot be committed.
    /// </returns>
    /// <exception cref="NotLeaderException">The current node is not a leader.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled or the cluster elects a new leader.</exception>
    Task<bool> RemoveMemberAsync(Uri address, CancellationToken token = default);

    /// <summary>
    /// Gets the address of the local member.
    /// </summary>
    Uri LocalMemberAddress { get; }
}