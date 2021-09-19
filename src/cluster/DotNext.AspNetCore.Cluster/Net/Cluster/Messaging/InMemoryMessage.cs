using System.Net.Mime;

namespace DotNext.Net.Cluster.Messaging;

using Buffers;
using IO;

// this is not a public class because it's designed for special purpose: bufferize content of another DTO.
// For that purpose we using growable buffer which relies on the pooled memory
internal sealed class InMemoryMessage : Disposable, IDataTransferObject, IBufferedMessage
{
    private readonly int initialSize;
    private MemoryOwner<byte> buffer;

    internal InMemoryMessage(string name, ContentType type, int initialSize)
    {
        Name = name;
        Type = type;
        this.initialSize = initialSize;
    }

    public string Name { get; }

    public ContentType Type { get; }

    bool IDataTransferObject.IsReusable => true;

    long? IDataTransferObject.Length => buffer.Length;

    private ReadOnlyMemory<byte> Content => buffer.Memory;

    public ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        where TWriter : IAsyncBinaryWriter
        => writer.WriteAsync(Content, null, token);

    async ValueTask IBufferedMessage.LoadFromAsync(IDataTransferObject source, CancellationToken token)
    {
        buffer.Dispose();
        buffer = await source.ToMemoryAsync(token: token).ConfigureAwait(false);
    }

    void IBufferedMessage.PrepareForReuse()
    {
    }

    ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
        => transformation.TransformAsync(IAsyncBinaryReader.Create(Content), token);

    bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
    {
        memory = buffer.Memory;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            buffer.Dispose();
        }

        base.Dispose(disposing);
    }

    ValueTask IAsyncDisposable.DisposeAsync() => DisposeAsync();
}