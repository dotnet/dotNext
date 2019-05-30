using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents cluster member accessible through Raft protocol.
    /// </summary>
    public interface IRaftClusterMember : IClusterMember, IDisposable
    {
        /// <summary>
        /// Requests vote from the member.
        /// </summary>
        /// <param name="callback">The callback to be invoked if status of this member is changed.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>Vote received from member; <see langword="true"/> if node accepts new leader, <see langword="false"/> if node doesn't accept new leader, <see langword="null"/> if node is not available.</returns>
        Task<bool?> Vote(ClusterMemberStatusChanged callback, CancellationToken token);

        /// <summary>
        /// Revokes leadership.
        /// </summary>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns><see langword="true"/>, if leadership is revoked successfully; otherwise, <see langword="false"/>.</returns>
        Task<bool> Resign(CancellationToken token);

        /// <summary>
        /// Aborts all active outbound requests.
        /// </summary>
        void CancelPendingRequests();
    }
}
