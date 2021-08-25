using System.ComponentModel;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;

    /// <summary>
    /// Represents log entry in Raft audit trail.
    /// </summary>
    public interface IRaftLogEntry : IO.Log.ILogEntry
    {
        /// <summary>
        /// Represents reserved command identifier for AddServer command.
        /// </summary>
        /// <seealso cref="Membership.AddMemberLogEntry"/>
        public const int AddMemberCommandId = int.MinValue;

        /// <summary>
        /// Represents reserved command identifier for RemoveServer command.
        /// </summary>
        /// <seealso cref="Membership.RemoveMemberLogEntry"/>
        public const int RemoveMemberCommandId = AddMemberCommandId + 1;

        /// <summary>
        /// Gets Term value associated with this log entry.
        /// </summary>
        long Term { get; }

        /// <summary>
        /// Represents identifier of the command encapsulated
        /// by this log entry.
        /// </summary>
        int? CommandId => null;

        /// <summary>
        /// Attempts to extract the list of cluster members in raw format.
        /// </summary>
        /// <remarks>
        /// This method decouples custom payload of the log entry from Raft-specific
        /// information.
        /// It is useful only if the underlying audit trail decouples configuration and the rest of the snapshot.
        /// If you are using custom implementation of audit trail then just store the configuration inside
        /// of the same snapshot with custom data and implement <see cref="Membership.IClusterConfigurationStorage"/>
        /// interface.
        /// </remarks>
        /// <param name="configuration">The buffer containing addresses of cluster members.</param>
        /// <returns><see langword="true"/> if the list obtained successfully; otherwise, <see langword="false"/>.</returns>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        bool TryGetClusterConfiguration(out MemoryOwner<byte> configuration)
        {
            configuration = default;
            return false;
        }
    }
}