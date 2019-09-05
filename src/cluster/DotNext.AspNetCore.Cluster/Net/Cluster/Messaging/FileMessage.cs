using System;
using System.IO;
using System.IO.Pipelines;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    internal sealed class FileMessage : IDisposableMessage
    {
        private readonly FileInfo file;

        private FileMessage(string name, ContentType type)
        {
            Name = name;
            Type = type;
            file = new FileInfo(Path.GetTempFileName());
        }

        internal static async Task<FileMessage> CreateAsync(IMessage source, CancellationToken token)
        {
            var result = new FileMessage(source.Name, source.Type);
            using (var stream = result.file.Create())
            {
                await source.CopyToAsync(stream, token).ConfigureAwait(false);
            }
            return result;
        }

        async Task IDataTransferObject.CopyToAsync(Stream output, CancellationToken token)
        {
            using (var stream = file.Open(FileMode.Open))
            {
                await stream.CopyToAsync(output, 1024, token).ConfigureAwait(false);
            }
        }

        async ValueTask IDataTransferObject.CopyToAsync(PipeWriter output, CancellationToken token)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            using (var stream = file.Open(FileMode.Open))
            using (var message = new StreamMessage(stream, true, Name, Type))
            {
                await ((IMessage)message).CopyToAsync(output, token).ConfigureAwait(false);
            }
        }

        long? IDataTransferObject.Length => file.Length;

        bool IDataTransferObject.IsReusable => false;

        public string Name { get; }

        public ContentType Type { get; }

        void IDisposable.Dispose() => file.Delete();
    }
}