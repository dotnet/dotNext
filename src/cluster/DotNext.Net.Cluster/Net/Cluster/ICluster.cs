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
    /// An event raised when leader has been changed.
    /// </summary>
    event Action<ICluster, IClusterMember?> LeaderChanged;

    /// <summary>
    /// Revokes leadership and starts new election process.
    /// </summary>
    /// <returns><see langword="true"/> if leadership is revoked successfully; otherwise, <see langword="false"/>.</returns>
    Task<bool> ResignAsync(CancellationToken token);
}