using System.Diagnostics.CodeAnalysis;

namespace DotNext.IO;

[ExcludeFromCodeCoverage]
internal class BinaryTransferObject(ReadOnlyMemory<byte> content) : IDataTransferObject
{
    internal ReadOnlyMemory<byte> Content => content;

    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => writer.Invoke(content, token);

    bool IDataTransferObject.IsReusable => true;

    long? IDataTransferObject.Length => content.Length;
}