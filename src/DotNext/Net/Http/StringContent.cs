using System.Net;
using System.Net.Mime;
using System.Text;

namespace DotNext.Net.Http
{
    using Buffers;
    using Text;

    /// <summary>
    /// Provides HTTP content based on plain string.
    /// </summary>
    public sealed class StringContent : HttpContent
    {
        private readonly ReadOnlyMemory<char> content;
        private readonly Encoding encoding;
        private readonly MemoryAllocator<byte>? allocator;

        /// <summary>
        /// Initializes a new content based on plain string.
        /// </summary>
        /// <param name="content">A set of characters to be passed through HTTP protocol.</param>
        /// <param name="encoding">The encoding of the characters. Default is UTF-8.</param>
        /// <param name="allocator">The memory allocator needed for characters encoding.</param>
        public StringContent(ReadOnlyMemory<char> content, Encoding? encoding = null, MemoryAllocator<byte>? allocator = null)
        {
            this.content = content;
            this.encoding = encoding ?? Encoding.UTF8;
            Headers.ContentType = new(MediaTypeNames.Text.Plain) { CharSet = this.encoding.WebName };
        }

        /// <inheritdoc />
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token)
        {
            using var bytes = encoding.GetBytes(content.Span, allocator);
            await stream.WriteAsync(bytes.Memory, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => SerializeToStreamAsync(stream, context, CancellationToken.None);

        /// <inheritdoc />
        protected override bool TryComputeLength(out long length)
        {
            length = encoding.GetByteCount(content.Span);
            return true;
        }
    }
}