using System.Net.Mime;

namespace DotNext.Net.Cluster.Messaging;

using IO;

internal sealed class FileMessage(string name, ContentType type) : FileStream(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose), IBufferedMessage
{
    private const int BufferSize = 1024;
    internal const int MinSize = 10 * 10 * 1024;   // 100 KB

    async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
    {
        try
        {
            await writer.CopyFromAsync(this, count: null, token).ConfigureAwait(false);
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

    string IMessage.Name => name;

    public ContentType Type => type;
}