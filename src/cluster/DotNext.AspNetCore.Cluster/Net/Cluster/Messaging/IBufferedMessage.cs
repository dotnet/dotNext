namespace DotNext.Net.Cluster.Messaging;

using IDataTransferObject = IO.IDataTransferObject;

internal interface IBufferedMessage : IDisposableMessage
{
    ValueTask LoadFromAsync(IDataTransferObject source, CancellationToken token);

    void PrepareForReuse();
}