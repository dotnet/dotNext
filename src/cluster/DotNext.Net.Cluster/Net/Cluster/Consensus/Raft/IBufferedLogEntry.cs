namespace DotNext.Net.Cluster.Consensus.Raft;

using IO;

internal interface IBufferedLogEntry : IInputLogEntry
{
    ReadOnlyMemory<byte> Content { get; }

    long? IDataTransferObject.Length => Content.Length;

    bool IDataTransferObject.IsReusable => true;

    bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
    {
        memory = Content;
        return true;
    }
}