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
    /// It cannot be shared across parallel flows or threads. However, you can call <see cref="Copy"/> method to create
    /// an independent copy of this context for separated async flow or thread.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct EncodingContext : ICloneable
    {
        private readonly Encoding encoding;
        private readonly Encoder? encoder;

        /// <summary>
        /// Initializes a new encoding context.
        /// </summary>
        /// <param name="encoding">The encoding to be used for converting string into bytes.</param>
        /// <param name="reuseEncoder"><see langword="true"/> to reuse the encoder between encoding operations; <see langword="false"/> to create separated encoder for each encoding operation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="encoding"/> is <see langword="null"/>.</exception>
        public EncodingContext(Encoding encoding, bool reuseEncoder)
        {
            this.encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            encoder = reuseEncoder ? encoding.GetEncoder() : null;
        }

        /// <summary>
        /// Creates independent copy of this context that can be used
        /// in separated async flow or thread.
        /// </summary>
        /// <returns>The independent copy of this context.</returns>
        public EncodingContext Copy() => new (encoding, encoder is not null);

        /// <inheritdoc/>
        object ICloneable.Clone() => Copy();

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
        /// Creates encoding context.
        /// </summary>
        /// <param name="encoding">The text encoding.</param>
        public static implicit operator EncodingContext(Encoding encoding) => new (encoding, false);
    }
}
