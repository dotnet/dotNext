using System.IO;
using System.IO.Pipelines;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    internal sealed class FileMessage : FileStream, IDisposableMessage
    {
        private const int BufferSize = 1024;
        private readonly string messageName;

        internal FileMessage(string name, ContentType type)
            : base(Path.GetTempFileName(), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 1024, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose)
        {
            messageName = name;
            Type = type;
        }

        Task IDataTransferObject.CopyToAsync(Stream output, CancellationToken token) => CopyToAsync(output, BufferSize, token);

        async ValueTask IDataTransferObject.CopyToAsync(PipeWriter output, CancellationToken token)
        {
            using var message = output.AsStream(true);
            await CopyToAsync(message, BufferSize, token).ConfigureAwait(false);
        }

        long? IDataTransferObject.Length => Length;

        bool IDataTransferObject.IsReusable => false;

        string IMessage.Name => messageName;

        public ContentType Type { get; }
    }
}