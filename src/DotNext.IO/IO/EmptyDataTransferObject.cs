namespace DotNext.IO;

internal sealed class EmptyDataTransferObject : IDataTransferObject
{
    internal static readonly EmptyDataTransferObject Instance = new();

    private EmptyDataTransferObject()
    {
    }

    bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
    {
        memory = ReadOnlyMemory<byte>.Empty;
        return true;
    }

    bool IDataTransferObject.IsReusable => true;

    long? IDataTransferObject.Length => 0L;

    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => ValueTask.CompletedTask;

    ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
        => transformation.TransformAsync(EmptyBinaryReader.Instance, token);
}