using System.Runtime.InteropServices;

namespace DotNext.IO;

/// <summary>
/// Represents binary transfer object.
/// </summary>
/// <param name="content">The payload of the object.</param>
[StructLayout(LayoutKind.Auto)]
public readonly struct BinaryTransferObject(ReadOnlyMemory<byte> content) : IDataTransferObject
{
    /// <summary>
    /// Gets the payload.
    /// </summary>
    public ReadOnlyMemory<byte> Content => content;

    /// <inheritdoc/>
    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => writer.Invoke(content, token);

    /// <inheritdoc/>
    bool IDataTransferObject.IsReusable => true;

    /// <inheritdoc/>
    long? IDataTransferObject.Length => content.Length;

    /// <inheritdoc/>
    bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
    {
        memory = content;
        return true;
    }
}