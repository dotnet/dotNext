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

        internal static async Task<FileMessage> CreateAsync(IMessage source)
        {
            var result = new FileMessage(source.Name, source.Type);
            using (var stream = result.file.Create())
            {
                await source.CopyToAsync(stream).ConfigureAwait(false);
            }
            return result;
        }

        async Task IMessage.CopyToAsync(Stream output)
        {
            using (var stream = file.Open(FileMode.Open))
            {
                await stream.CopyToAsync(output).ConfigureAwait(false);
            }
        }

        async ValueTask IMessage.CopyToAsync(PipeWriter output, CancellationToken token)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            using (var stream = file.Open(FileMode.Open))
            using (var message = new StreamMessage(stream, true, Name, Type))
            {
                await ((IMessage)message).CopyToAsync(output, token).ConfigureAwait(false);
            }
        }

        long? IMessage.Length => file.Length;

        bool IMessage.IsReusable => false;

        public string Name { get; }

        public ContentType Type { get; }

        void IDisposable.Dispose() => file.Delete();
    }
}