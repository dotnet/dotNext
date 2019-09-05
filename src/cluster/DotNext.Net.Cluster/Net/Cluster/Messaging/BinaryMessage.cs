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
    public class BinaryMessage : IMessage
    {
        /// <summary>
        /// Initializes a new binary message.
        /// </summary>
        /// <param name="content">The content of the message.</param>
        /// <param name="name">The name of the message.</param>
        /// <param name="type">Media type of the message content.</param>
        public BinaryMessage(ReadOnlySequence<byte> content, string name, ContentType type = null)
        {
            Type = type;
            Name = name;
            Content = content;
        }

        /// <summary>
        /// Initializes a new binary message.
        /// </summary>
        /// <param name="content">The content of the message.</param>
        /// <param name="name">The name of the message.</param>
        /// <param name="type">Media type of the message content.</param>
        public BinaryMessage(ReadOnlyMemory<byte> content, string name, ContentType type = null)
            : this(new ReadOnlySequence<byte>(content), name, type)
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

        bool IDataTransferObject.IsReusable => true;

        long? IDataTransferObject.Length => Content.Length;

        /// <summary>
        /// Gets media type of the message.
        /// </summary>
        public ContentType Type { get; }

        async Task IDataTransferObject.CopyToAsync(Stream output)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            foreach (var segment in Content)
                using (var array = new ArrayRental<byte>(segment.Length))
                {
                    segment.CopyTo(array.Memory);
                    await output.WriteAsync(array, 0, segment.Length).ConfigureAwait(false);
                }
        }

        async ValueTask IDataTransferObject.CopyToAsync(PipeWriter output, CancellationToken token)
        {
            foreach (var segment in Content)
            {
                var result = await output.WriteAsync(segment, token);
                if (result.IsCompleted)
                    break;
                if (result.IsCanceled)
                    throw new OperationCanceledException(token);
                result = await output.FlushAsync(token);
                if (result.IsCompleted)
                    break;
                if (result.IsCanceled)
                    throw new OperationCanceledException(token);
            }
        }
    }
}
