using System.IO;
using System.IO.Pipelines;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    using IO;

    internal sealed class FileMessage : FileStream, IBufferedMessage
    {
        internal const int MinSize = 10 * 10 * 1024;   //100 KB
        private readonly string messageName;

        internal FileMessage(string name, ContentType type)
            : base(Path.GetTempFileName(), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 1024, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose)
        {
            messageName = name;
            Type = type;
        }

        ValueTask IDataTransferObject.CopyToAsync(Stream output, CancellationToken token) => new ValueTask(CopyToAsync(output, token));

        ValueTask IDataTransferObject.CopyToAsync(PipeWriter output, CancellationToken token) => new ValueTask(this.CopyToAsync(output, token));

        ValueTask IBufferedMessage.LoadFromAsync(IDataTransferObject source, CancellationToken token) => source.CopyToAsync(this, token);

        void IBufferedMessage.PrepareForReuse() => Seek(0L, SeekOrigin.Begin);

        long? IDataTransferObject.Length => Length;

        bool IDataTransferObject.IsReusable => false;

        string IMessage.Name => messageName;

        public ContentType Type { get; }
    }
}