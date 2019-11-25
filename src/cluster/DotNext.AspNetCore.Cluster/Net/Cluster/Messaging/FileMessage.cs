using System.IO;
using System.IO.Pipelines;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    using IO;

    internal sealed class FileMessage : FileStream, IDisposableMessage
    {
        private readonly string messageName;

        internal FileMessage(string name, ContentType type)
            : base(Path.GetTempFileName(), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 1024, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose)
        {
            messageName = name;
            Type = type;
        }

        ValueTask IDataTransferObject.CopyToAsync(Stream output, CancellationToken token) => new ValueTask(CopyToAsync(output, token));

        ValueTask IDataTransferObject.CopyToAsync(PipeWriter output, CancellationToken token) => new ValueTask(this.CopyToAsync(output, token));

        long? IDataTransferObject.Length => Length;

        bool IDataTransferObject.IsReusable => false;

        string IMessage.Name => messageName;

        public ContentType Type { get; }
    }
}