namespace DotNext.Net.Cluster;

/// <summary>
/// Represents local view of the entire cluster.
/// </summary>
public interface ICluster : IPeerMesh<IClusterMember>
{
    /// <summary>
    /// Gets the leader node.
    /// </summary>
    IClusterMember? Leader { get; }

    /// <summary>
    /// Waits for the leader election asynchronously.
    /// </summary>
    /// <param name="timeout">The time to wait; or <see cref="Timeout.InfiniteTimeSpan"/>.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The elected leader or <see langword="null"/> if the cluster losts the leader.</returns>
    /// <exception cref="TimeoutException">The operation is timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The local node is disposed.</exception>
    Task<IClusterMember?> WaitForLeaderAsync(TimeSpan timeout, CancellationToken token = default);

    /// <summary>
    /// An event raised when leader has been changed.
    /// </summary>
    event Action<ICluster, IClusterMember?> LeaderChanged;

    /// <summary>
    /// Revokes leadership and starts new election process.
    /// </summary>
    /// <returns><see langword="true"/> if leadership is revoked successfully; otherwise, <see langword="false"/>.</returns>
    Task<bool> ResignAsync(CancellationToken token);
}