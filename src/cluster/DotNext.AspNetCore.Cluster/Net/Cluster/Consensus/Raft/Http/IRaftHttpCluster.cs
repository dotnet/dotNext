namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using Messaging;

/// <summary>
/// Represents local view of Raft cluster built on top of ASP.NET Core infrastructure.
/// </summary>
public interface IRaftHttpCluster : IRaftCluster, IMessageBus
{
    /// <summary>
    /// Announces a new member in the cluster.
    /// </summary>
    /// <param name="id">The identifier of the cluster member.</param>
    /// <param name="address">The addres of the cluster member.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the node has been added to the cluster successfully;
    /// <see langword="false"/> if the node rejects the replication or the address of the node cannot be committed.
    /// </returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled or the cluster elects a new leader.</exception>
    Task<bool> AddMemberAsync(ClusterMemberId id, Uri address, CancellationToken token = default);

    /// <summary>
    /// Removes the member from the cluster.
    /// </summary>
    /// <param name="id">The cluster member to remove.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the node has been removed from the cluster successfully;
    /// <see langword="false"/> if the node rejects the replication or the address of the node cannot be committed.
    /// </returns>
    Task<bool> RemoveMemberAsync(ClusterMemberId id, CancellationToken token = default);

    /// <summary>
    /// Removes the member from the cluster.
    /// </summary>
    /// <param name="address">The cluster member to remove.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the node has been removed from the cluster successfully;
    /// <see langword="false"/> if the node rejects the replication or the address of the node cannot be committed.
    /// </returns>
    Task<bool> RemoveMemberAsync(Uri address, CancellationToken token = default);

    /// <summary>
    /// Gets the identifier of the local member.
    /// </summary>
    ref readonly ClusterMemberId LocalMemberId { get; }

    /// <summary>
    /// Gets the address of the local member.
    /// </summary>
    Uri LocalMemberAddress { get; }
}