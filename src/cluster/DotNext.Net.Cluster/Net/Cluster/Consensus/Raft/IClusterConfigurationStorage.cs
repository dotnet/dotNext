using System;
using System.Buffers;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents a feature of <see cref="IPersistentState"/>
    /// that allows to store cluster membership directly in the log.
    /// </summary>
    public interface IClusterConfigurationStorage : IPersistentState
    {
        /// <summary>
        /// Handles cluster configuration stored in Raft log.
        /// </summary>
        public interface IMembershipChangeHandler
        {
            /// <summary>
            /// Called automatically when a new node has to be added.
            /// </summary>
            /// <remarks>
            /// This method is called automatically by the log
            /// when it applies the log entry with <see cref="IRaftLogEntry.AddServerCommandId"/>
            /// identifier.
            /// </remarks>
            /// <param name="address">Node address in raw format.</param>
            /// <param name="configuration">The writer for the updated configuration including.</param>
            /// <returns>The task representing asynchronous result.</returns>
            ValueTask AddNodeAsync(ReadOnlyMemory<byte> address, IBufferWriter<byte> configuration);

            /// <summary>
            /// Called automatically when the existing node has to be removed.
            /// </summary>
            /// <remarks>
            /// This method is called automatically by the log
            /// when it applies the log entry with <see cref="IRaftLogEntry.RemoveServerCommandId"/>
            /// identifier.
            /// </remarks>
            /// <param name="address">Node address in raw format.</param>
            /// <param name="configuration">The writer for the updated configuration including.</param>
            /// <returns>The task representing asynchronous result.</returns>
            ValueTask RemoveNodeAsync(ReadOnlyMemory<byte> address, IBufferWriter<byte> configuration);

            /// <summary>
            /// Applies the list of cluster members.
            /// </summary>
            /// <param name="configuration">The list of cluster members.</param>
            /// <returns>The task representing asynchronous result.</returns>
            ValueTask ApplyAsync(ReadOnlyMemory<byte> configuration);
        }

        /// <summary>
        /// Gets or sets the configuration change tracker.
        /// </summary>
        IMembershipChangeHandler? MembershipChangeHandler { get; set; }
    }
}