using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Represents cluster node in distributed environment.
    /// </summary>
    public interface ICluster : IReadOnlyCollection<IClusterMember>
    {
        /// <summary>
        /// Gets the leader node.
        /// </summary>
        IClusterMember Leader { get; }

        /// <summary>
        /// Gets cluster member represented by entire application.
        /// </summary>
        IClusterMember LocalMember { get; }

        /// <summary>
        /// An event raised when leader has been changed.
        /// </summary>
        event ClusterLeaderChangedEventHandler LeaderChanged;

        /// <summary>
        /// An event raised when cluster member becomes available or unavailable.
        /// </summary>
        event ClusterMemberStatusChanged MemberStatusChanged;

        /// <summary>
        /// Revokes leadership and starts new election process.
        /// </summary>
        /// <returns><see langword="true"/> if leadership is revoked successfully; otherwise, <see langword="false"/>.</returns>
        Task<bool> ResignAsync(CancellationToken token);
    }
}
