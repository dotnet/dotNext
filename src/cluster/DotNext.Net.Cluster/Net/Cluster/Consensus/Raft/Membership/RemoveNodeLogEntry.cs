using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership
{
    using Buffers;
    using IO;

    /// <summary>
    /// Represents a special log entry that contains the address of
    /// the existing node to be removed from the cluster.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct RemoveMemberLogEntry : IRaftLogEntry
    {
        private readonly ClusterMemberId memberId;

        /// <summary>
        /// Initializes a new log entry.
        /// </summary>
        /// <param name="memberId">The unique identifier of the cluster node.</param>
        /// <param name="term">The current term of the leader.</param>
        public RemoveMemberLogEntry(ClusterMemberId memberId, long term)
        {
            this.memberId = memberId;
            Term = term;
            Timestamp = DateTimeOffset.UtcNow;
        }

        /// <inheritdoc />
        int? IRaftLogEntry.CommandId => IRaftLogEntry.RemoveMemberCommandId;

        /// <summary>
        /// Gets the term associated with this log entry.
        /// </summary>
        public long Term { get; }

        /// <summary>
        /// Gets timestamp of this log entry.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <inheritdoc />
        long? IDataTransferObject.Length => ClusterMemberId.Size;

        /// <inheritdoc />
        bool IDataTransferObject.IsReusable => true;

        /// <inheritdoc />
        bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
        {
            memory = default;
            return false;
        }

        /// <inheritdoc />
        async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            using var serializedId = memberId.Bufferize();
            await writer.WriteAsync(serializedId.Memory, null, token).ConfigureAwait(false);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct Deserializer : IDataTransferObject.ITransformation<ClusterMemberId>
        {
            private readonly MemoryAllocator<byte>? allocator;

            internal Deserializer(MemoryAllocator<byte>? allocator)
                => this.allocator = allocator;

            async ValueTask<ClusterMemberId> IDataTransferObject.ITransformation<ClusterMemberId>.TransformAsync<TReader>(TReader reader, CancellationToken token)
            {
                using var buffer = allocator.Invoke(ClusterMemberId.Size, true);
                await reader.ReadAsync(buffer.Memory, token).ConfigureAwait(false);
                return new(buffer.Memory.Span);
            }
        }
    }
}