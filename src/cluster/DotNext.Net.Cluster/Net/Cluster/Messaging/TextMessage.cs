using System;
using System.IO;
using System.IO.Pipelines;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    using Buffers;
    using IO;
    using static Mime.ContentTypeExtensions;

    /// <summary>
    /// Represents text message.
    /// </summary>
    public class TextMessage : IMessage
    {
        /// <summary>
        /// Initializes a new text message.
        /// </summary>
        /// <param name="value">The message content.</param>
        /// <param name="name">The name of the message.</param>
        public TextMessage(string value, string name)
            : this(name, value, null)
        {

        }

        internal TextMessage(string name, string value, string mediaType)
        {
            Content = value;
            Type = new ContentType() { MediaType = mediaType.IfNullOrEmpty(MediaTypeNames.Text.Plain), CharSet = "utf-8" };
            Name = name;
        }

        /// <summary>
        /// Gets name of this message.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets content length, in bytes.
        /// </summary>
        public int Length => Type.GetEncoding().GetByteCount(Content);

        bool IDataTransferObject.IsReusable => true;

        long? IDataTransferObject.Length => Length;

        /// <summary>
        /// The message content.
        /// </summary>
        public string Content { get; }

        async Task IDataTransferObject.CopyToAsync(Stream output, CancellationToken token)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            const int defaultBufferSize = 128;
            using (var buffer = new ArrayRental<byte>(defaultBufferSize))
                await output.WriteStringAsync(Content, Type.GetEncoding(), (byte[])buffer, token).ConfigureAwait(false);
        }

        private static unsafe int Encode(Encoding encoding, ReadOnlySpan<char> chunk, Span<byte> output)
        {
            fixed (char* source = chunk)
            fixed (byte* dest = output)
            {
                return encoding.GetBytes(source, chunk.Length, dest, output.Length);
            }
        }

        async ValueTask IDataTransferObject.CopyToAsync(PipeWriter output, CancellationToken token)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            var encoding = Type.GetEncoding();
            var bytesCount = encoding.GetByteCount(Content);
            var buffer = output.GetMemory(bytesCount);
            Encode(encoding, Content.AsSpan(), buffer.Span);
            output.Advance(bytesCount);
            var result = await output.FlushAsync(token);
            if (result.IsCanceled)
                throw new OperationCanceledException(token);
        }

        /// <summary>
        /// MIME type of the message.
        /// </summary>
        public ContentType Type { get; }
    }
}