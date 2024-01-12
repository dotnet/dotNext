namespace DotNext.IO;

using Patterns;

internal sealed class EmptyDataTransferObject : IDataTransferObject, ISingleton<EmptyDataTransferObject>
{
    public static EmptyDataTransferObject Instance { get; } = new();

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