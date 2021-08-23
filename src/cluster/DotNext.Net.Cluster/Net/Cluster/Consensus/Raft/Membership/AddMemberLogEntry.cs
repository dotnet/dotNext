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
    /// a new node to be added to the cluster.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct AddMemberLogEntry : IRaftLogEntry
    {
        private readonly ClusterMemberId memberId;
        private readonly ReadOnlyMemory<byte> address;

        /// <summary>
        /// Initializes a new log entry.
        /// </summary>
        /// <param name="memberId">The unique identifier of the cluster node.</param>
        /// <param name="address">The address of the cluster node in raw format.</param>
        /// <param name="term">The current term of the leader.</param>
        public AddMemberLogEntry(ClusterMemberId memberId, ReadOnlyMemory<byte> address, long term)
        {
            this.memberId = memberId;
            this.address = address;
            Term = term;
            Timestamp = DateTimeOffset.UtcNow;
        }

        /// <inheritdoc />
        int? IRaftLogEntry.CommandId => IRaftLogEntry.AddMemberCommandId;

        /// <summary>
        /// Gets the term associated with this log entry.
        /// </summary>
        public long Term { get; }

        /// <summary>
        /// Gets timestamp of this log entry.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <inheritdoc />
        long? IDataTransferObject.Length => ClusterMemberId.Size + address.Length;

        /// <inheritdoc />
        bool IDataTransferObject.IsReusable => true;

        /// <inheritdoc />
        bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
        {
            memory = default;
            return false;
        }

        async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            using var serializedId = memberId.Bufferize();
            await writer.WriteAsync(serializedId.Memory, null, token).ConfigureAwait(false);
            await writer.WriteAsync(address, LengthFormat.PlainLittleEndian, token).ConfigureAwait(false);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct Deserializer : IDataTransferObject.ITransformation<(ClusterMemberId, MemoryOwner<byte>)>
        {
            private readonly MemoryAllocator<byte>? allocator;

            internal Deserializer(MemoryAllocator<byte>? allocator)
                => this.allocator = allocator;

            async ValueTask<(ClusterMemberId, MemoryOwner<byte>)> IDataTransferObject.ITransformation<(ClusterMemberId, MemoryOwner<byte>)>.TransformAsync<TReader>(TReader reader, CancellationToken token)
            {
                ClusterMemberId id;
                using (var buffer = allocator.Invoke(ClusterMemberId.Size, true))
                {
                    await reader.ReadAsync(buffer.Memory, token).ConfigureAwait(false);
                    id = new(buffer.Memory.Span);
                }

                var address = await reader.ReadAsync(LengthFormat.PlainLittleEndian, allocator, token).ConfigureAwait(false);
                return (id, address);
            }
        }
    }
}