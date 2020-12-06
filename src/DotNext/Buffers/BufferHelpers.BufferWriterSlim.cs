using System.IO;
using System.Text;

namespace DotNext.Buffers
{
    public static partial class BufferHelpers
    {
        /// <summary>
        /// Copies written bytes to the stream.
        /// </summary>
        /// <param name="builder">The buffer builder.</param>
        /// <param name="output">The output stream.</param>
        public static unsafe void CopyTo(this in BufferWriterSlim<byte> builder, Stream output)
            => builder.CopyTo(new (&Span.CopyTo), output);

        /// <summary>
        /// Copies written characters to the text stream.
        /// </summary>
        /// <param name="builder">The buffer builder.</param>
        /// <param name="output">The output stream.</param>
        public static unsafe void CopyTo(this in BufferWriterSlim<char> builder, TextWriter output)
            => builder.CopyTo(new (&Span.CopyTo), output);

        /// <summary>
        /// Copies written characters to string builder.
        /// </summary>
        /// <param name="builder">The buffer builder.</param>
        /// <param name="output">The string builder.</param>
        public static unsafe void CopyTo(this in BufferWriterSlim<char> builder, StringBuilder output)
            => builder.CopyTo(new (&Span.CopyTo), output);
    }
}