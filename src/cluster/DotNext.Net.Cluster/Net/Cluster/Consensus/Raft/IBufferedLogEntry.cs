namespace DotNext.Net.Cluster.Consensus.Raft;

using IO;

internal interface IBufferedLogEntry : IInputLogEntry
{
    ReadOnlySpan<byte> Content { get; }

    long? IDataTransferObject.Length => Content.Length;

    bool IDataTransferObject.IsReusable => true;
}