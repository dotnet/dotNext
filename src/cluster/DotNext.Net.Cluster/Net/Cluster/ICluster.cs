using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster
{
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
        /// Gets collection of cluster members.
        /// </summary>
        IReadOnlyCollection<IClusterMember> Members { get; } // TODO: Replace with Peers property in .NEXT 4

        /// <summary>
        /// An event raised when leader has been changed.
        /// </summary>
        event ClusterLeaderChangedEventHandler? LeaderChanged;

        /// <summary>
        /// Revokes leadership and starts new election process.
        /// </summary>
        /// <returns><see langword="true"/> if leadership is revoked successfully; otherwise, <see langword="false"/>.</returns>
        Task<bool> ResignAsync(CancellationToken token);

        /// <inheritdoc/>
        IReadOnlyCollection<EndPoint> IPeerMesh.Peers => Members.Select(static member => member.EndPoint).ToArray();

        /// <inheritdoc/>
        IClusterMember? IPeerMesh<IClusterMember>.TryGetPeer(EndPoint peer)
        {
            foreach (var member in Members)
            {
                if (Equals(member.EndPoint, peer))
                    return member;
            }

            return null;
        }
    }
}
