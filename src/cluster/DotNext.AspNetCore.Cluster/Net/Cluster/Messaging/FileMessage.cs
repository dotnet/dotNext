using System.Net.Mime;

namespace DotNext.Net.Cluster.Messaging;

using IO;

internal sealed class FileMessage : FileStream, IBufferedMessage
{
    private const int BufferSize = 1024;
    internal const int MinSize = 10 * 10 * 1024;   // 100 KB
    private readonly string messageName;

    internal FileMessage(string name, ContentType type)
        : base(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose)
    {
        messageName = name;
        Type = type;
    }

    async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
    {
        try
        {
            await writer.CopyFromAsync(this, token).ConfigureAwait(false);
        }
        finally
        {
            Seek(0L, SeekOrigin.Begin);
        }
    }

    ValueTask IBufferedMessage.LoadFromAsync(IDataTransferObject source, CancellationToken token)
    {
        if (source.Length.TryGetValue(out var length))
            SetLength(length);

        return source.WriteToAsync(this, BufferSize, token);
    }

    void IBufferedMessage.PrepareForReuse() => Seek(0L, SeekOrigin.Begin);

    long? IDataTransferObject.Length => Length;

    bool IDataTransferObject.IsReusable => false;

    string IMessage.Name => messageName;

    public ContentType Type { get; }
}