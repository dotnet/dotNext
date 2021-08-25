using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership
{
    /// <summary>
    /// Represents a feature of <see cref="IPersistentState"/>
    /// that allows to store cluster membership directly in the log.
    /// </summary>
    public interface IClusterConfigurationStorage : IPersistentState // TODO: Merge with IPersistentState in .NEXT 4
    {
        /// <summary>
        /// Allows to track membership commands in Raft log.
        /// </summary>
        public interface IConfigurationInterpreter
        {
            /// <summary>
            /// Informs that a new member has to be added to the cluster.
            /// </summary>
            /// <param name="id">The identifier of the cluster member.</param>
            /// <param name="address">The address of the cluster member in raw format.</param>
            /// <returns>The task representing asynchronous result.</returns>
            ValueTask AddMemberAsync(ClusterMemberId id, ReadOnlyMemory<byte> address);

            /// <summary>
            /// Informs that the existing member has to be removed from the cluster.
            /// </summary>
            /// <param name="id">The identifier of the cluster member.</param>
            /// <returns>The task representing asynchronous result.</returns>
            ValueTask RemoveMemberAsync(ClusterMemberId id);

            /// <summary>
            /// Reloads a list of cluster members.
            /// </summary>
            /// <param name="members">A collection of cluster members.</param>
            /// <param name="token">The token that can be used to cancel the operation.</param>
            /// <returns>The task representing asynchronous result.</returns>
            ValueTask RefreshAsync(IAsyncEnumerable<KeyValuePair<ClusterMemberId, ReadOnlyMemory<byte>>> members, CancellationToken token = default);
        }

        /// <summary>
        /// Gets or sets a configuration tracker.
        /// </summary>
        IConfigurationInterpreter? ConfigurationInterpreter { get; set; }
    }
}