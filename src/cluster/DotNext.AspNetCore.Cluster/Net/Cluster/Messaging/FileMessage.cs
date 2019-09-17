using System.IO;
using System.IO.Pipelines;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    internal sealed class FileMessage : FileStream, IDisposableMessage
    {
        private readonly string messageName;

        internal FileMessage(string name, ContentType type)
            : base(Path.GetTempFileName(), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 1024, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose)
        {
            messageName = name;
            Type = type;
        }

        Task IDataTransferObject.CopyToAsync(Stream output, CancellationToken token) => CopyToAsync(output);

        async ValueTask IDataTransferObject.CopyToAsync(PipeWriter output, CancellationToken token)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            using (var message = new StreamMessage(this, true, messageName, Type))
            {
                await ((IMessage)message).CopyToAsync(output, token).ConfigureAwait(false);
            }
        }

        long? IDataTransferObject.Length => Length;

        bool IDataTransferObject.IsReusable => false;

        string IMessage.Name => messageName;

        public ContentType Type { get; }
    }
}