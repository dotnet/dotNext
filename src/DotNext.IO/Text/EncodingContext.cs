using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.Text
{
    /// <summary>
    /// Represents text encoding context.
    /// </summary>
    /// <remarks>
    /// The context represents an encoding cache to avoid memory allocations for each round of string encoding caused by methods of <see cref="IO.StreamExtensions"/> class.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct EncodingContext
    {
        private readonly Encoding encoding;
        private readonly Encoder encoder;

        /// <summary>
        /// Initializes a new encoding context.
        /// </summary>
        /// <param name="encoding">The encoding to be used for converting string into bytes.</param>
        /// <param name="reuseEncoder"><see langword="true"/> to reuse the encoder between encoding operations; <see langword="false"/> to create separated encoder for each encoding operation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="encoding"/> is <see langword="null"/>.</exception>
        public EncodingContext(Encoding encoding, bool reuseEncoder = false)
        {
            this.encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            encoder = reuseEncoder ? encoding.GetEncoder() : null;
        }

        /// <summary>
        /// Sets the encapsulated encoder to its initial state.
        /// </summary>
        public void Reset() => encoder?.Reset();

        /// <summary>
        /// Gets encoding associated with this context.
        /// </summary>
        public Encoding Encoding => encoding ?? Encoding.Default;

        internal Encoder GetEncoder() => encoder ?? Encoding.GetEncoder();

        /// <summary>
        /// Creates encoding context 
        /// </summary>
        /// <param name="encoding">The text encoding.</param>
        public static implicit operator EncodingContext(Encoding encoding) => new EncodingContext(encoding);
    }
}
