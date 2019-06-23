using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    using Buffers;

    /// <summary>
    /// Represents binary message.
    /// </summary>
    public sealed class BinaryMessage : IMessage
    {
        private const int BufferSize = 1024;

        /// <summary>
        /// Initializes a new binary message.
        /// </summary>
        /// <param name="name">The name of the message.</param>
        /// <param name="content">The content of the message.</param>
        /// <param name="type">Media type of the message content.</param>
        public BinaryMessage(string name, ReadOnlySequence<byte> content, ContentType type = null)
        {
            Type = type;
            Name = name;
            Content = content;
        }

        /// <summary>
        /// Initializes a new binary message.
        /// </summary>
        /// <param name="name">The name of the message.</param>
        /// <param name="content">The content of the message.</param>
        /// <param name="type">Media type of the message content.</param>
        public BinaryMessage(string name, ReadOnlyMemory<byte> content, ContentType type = null)
            : this(name, new ReadOnlySequence<byte>(content), type)
        {
        }

        /// <summary>
        /// Gets stream representing content.
        /// </summary>
        public ReadOnlySequence<byte> Content { get; }

        /// <summary>
        /// Gets name of the message.
        /// </summary>
        public string Name { get; }

        long? IMessage.Length => Content.Length;

        /// <summary>
        /// Gets media type of the message.
        /// </summary>
        public ContentType Type { get; }

        async Task IMessage.CopyToAsync(Stream output)
        {
            //TODO: Should be rewritte for .NET Standard 2.1
            foreach (var segment in Content)
                using (var array = new ArrayRental<byte>(segment.Length))
                {
                    segment.CopyTo(array.Memory);
                    await output.WriteAsync(array, 0, segment.Length).ConfigureAwait(false);
                }
        }

        async ValueTask IMessage.CopyToAsync(PipeWriter output, CancellationToken token)
        {
            foreach (var segment in Content)
            {
                var result = await output.WriteAsync(segment, token);
                if (result.IsCanceled || result.IsCompleted)
                    break;
                result = await output.FlushAsync(token);
                if (result.IsCanceled || result.IsCompleted)
                    break;
            }
            output.Complete(token.IsCancellationRequested ? new OperationCanceledException(token) : null);
        }
    }
}
