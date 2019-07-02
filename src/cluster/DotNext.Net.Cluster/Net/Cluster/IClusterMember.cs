using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Represents cluster member.
    /// </summary>
    public interface IClusterMember : IEquatable<IClusterMember>
    {
        /// <summary>
        /// Represents cluster member endpoint that can be used to send messages specific to consensus protocol.
        /// </summary>
        IPEndPoint Endpoint { get; }

        /// <summary>
        /// Indicates that executing host is a leader node in the cluster.
        /// </summary>
        bool IsLeader { get; }

        /// <summary>
        /// Indicates that this instance represents remote or local cluster member.
        /// </summary>
        bool IsRemote { get; }

        /// <summary>
        /// An event raised when cluster member becomes available or unavailable.
        /// </summary>
        event ClusterMemberStatusChanged MemberStatusChanged;

        /// <summary>
        /// Gets status of this member.
        /// </summary>
        ClusterMemberStatus Status { get; }

        /// <summary>
        /// Obtains metadata associated with this member.
        /// </summary>
        /// <param name="refresh"><see langword="true"/> to make a network request to the member and update local cache; <see langword="false"/> to obtain cached metadata.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <remarks>
        /// This method is completed synchronously is most cases if <paramref name="refresh"/> is <see langword="false"/>.
        /// </remarks>
        /// <returns>The task representing metadata read operation.</returns>
        /// <exception cref="MemberUnavailableException">This member is not reachable through the network.</exception>
        ValueTask<IReadOnlyDictionary<string, string>> GetMetadata(bool refresh = false, CancellationToken token = default);

        /// <summary>
        /// Revokes leadership.
        /// </summary>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns><see langword="true"/>, if leadership is revoked successfully; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="MemberUnavailableException">This member is not reachable through the network.</exception>
        Task<bool> ResignAsync(CancellationToken token);
    }
}
