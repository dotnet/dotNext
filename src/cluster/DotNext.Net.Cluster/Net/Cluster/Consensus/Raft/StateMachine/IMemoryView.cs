using System.Buffers;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

internal interface IMemoryView
{
    ReadOnlySequence<byte> GetSequence(ulong address, long length);

    bool TryGetMemory(ulong address, long length, out ReadOnlyMemory<byte> memory);

    IEnumerable<ReadOnlyMemory<byte>> EnumerateMemoryBlocks(ulong address, long length);
}