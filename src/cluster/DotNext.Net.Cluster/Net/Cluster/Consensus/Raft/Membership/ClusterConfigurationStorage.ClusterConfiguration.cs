using System.Collections.Immutable;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

using Buffers;
using IO;

partial class ClusterConfigurationStorage<TAddress>
{
    private sealed class ClusterConfiguration(ClusterConfigurationStorage<TAddress> storage, ImmutableHashSet<TAddress> members)
        : IClusterConfiguration<TAddress>, ISupplier<MemoryAllocator<byte>, MemoryOwner<byte>>
    {
        /// <inheritdoc/>
        public IReadOnlySet<TAddress> Members => members;

        private ClusterConfiguration Create(ImmutableHashSet<TAddress> copy)
            => ReferenceEquals(copy, members) ? this : new(storage, copy);

        public IClusterConfiguration<TAddress> Add(TAddress address) => Create(members.Add(address));

        public IClusterConfiguration<TAddress> Remove(TAddress address) => Create(members.Remove(address));

        /// <inheritdoc/>
        bool IDataTransferObject.IsReusable => true;

        /// <inheritdoc/>
        long? IDataTransferObject.Length => members.IsEmpty ? sizeof(int) : null;

        bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
        {
            memory = int.ZeroBytes; // length prefix
            return members.Count is 0;
        }

        /// <inheritdoc/>
        async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            var owner = CopyToMemory();
            try
            {
                await writer.WriteAsync(owner.Memory, token: token).ConfigureAwait(false);
            }
            finally
            {
                owner.Dispose();
            }
        }

        MemoryOwner<byte> ISupplier<MemoryAllocator<byte>, MemoryOwner<byte>>.Invoke(MemoryAllocator<byte> allocator)
            => CopyToMemory(allocator);
        
        private MemoryOwner<byte> CopyToMemory(MemoryAllocator<byte> allocator)
        {
            var writer = new BufferWriterSlim<byte>(BufferSize, allocator);
            WriteTo(ref writer);
            return writer.DetachOrCopyBuffer();
        }

        public MemoryOwner<byte> CopyToMemory() => CopyToMemory(storage.allocator);

        private void WriteTo(ref BufferWriterSlim<byte> writer)
        {
            // impl must be synced with TryGetMemory
            writer.WriteLittleEndian(members.Count);
            foreach (var address in members)
            {
                storage.Encode(address, ref writer);
            }
        }

        async ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
        {
            var owner = CopyToMemory();
            try
            {
                return await transformation.TransformAsync(new SequenceReader(owner.Memory), token).ConfigureAwait(false);
            }
            finally
            {
                owner.Dispose();
            }
        }
    }
    
    private void Deserialize(ImmutableHashSet<TAddress>.Builder builder, ref SequenceReader reader)
    {
        for (var count = reader.ReadLittleEndian<int>(); count > 0; count--)
        {
            builder.Add(Decode(ref reader));
        }
    }

    private ClusterConfiguration Deserialize(ref SequenceReader reader)
    {
        ImmutableHashSet<TAddress> members;
        if (reader.IsEmpty)
        {
            members = ImmutableHashSet.Create(Comparer);
        }
        else
        {
            var builder = ImmutableHashSet.CreateBuilder(Comparer);
            Deserialize(builder, ref reader);
            members = builder.ToImmutable();
        }

        return new(this, members);
    }

    private ClusterConfiguration Deserialize(ReadOnlyMemory<byte> configuration)
    {
        var reader = new SequenceReader(configuration);
        return Deserialize(ref reader);
    }

    private protected MemoryOwner<byte> Serialize(IReadOnlySet<TAddress> members)
        => new ClusterConfiguration(this, members.ToImmutableHashSet(Comparer)).CopyToMemory();
}