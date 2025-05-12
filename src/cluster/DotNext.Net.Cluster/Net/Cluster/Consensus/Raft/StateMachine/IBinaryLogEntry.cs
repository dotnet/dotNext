using DotNext.IO;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

public interface IBinaryLogEntry : IRaftLogEntry
{
    void WriteTo(Span<byte> buffer);
    
    int Length { get; }

    /// <inheritdoc/>
    long? IDataTransferObject.Length => Length;
}