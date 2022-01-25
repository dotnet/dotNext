namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;

internal interface IBinaryLogEntry : IRaftLogEntry
{
    MemoryOwner<byte> ToBuffer(MemoryAllocator<byte> allocator);
}