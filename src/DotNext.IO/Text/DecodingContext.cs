﻿using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.Text
{
    /// <summary>
    /// Represents text decoding context.
    /// </summary>
    /// <remarks>
    /// The context represents a decoding cache to avoid memory allocations for each round of string decoding caused by methods of <see cref="IO.StreamExtensions"/> class.
    /// It cannot be shared across parallel flows or threads. However, you can call <see cref="Copy"/> method to create
    /// an independent copy of this context for separated async flow or thread.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct DecodingContext : ICloneable
    {
        private readonly Encoding encoding;
        private readonly Decoder? decoder;

        /// <summary>
        /// Initializes a new decoding context.
        /// </summary>
        /// <param name="encoding">The encoding to be used for converting bytes into string.</param>
        /// <param name="reuseDecoder"><see langword="true"/> to reuse the decoder between decoding operations; <see langword="false"/> to create separated encoder for each encoding operation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="encoding"/> is <see langword="null"/>.</exception>
        public DecodingContext(Encoding encoding, bool reuseDecoder)
        {
            this.encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            decoder = reuseDecoder ? encoding.GetDecoder() : null;
        }

        /// <summary>
        /// Creates independent copy of this context that can be used
        /// in separated async flow or thread.
        /// </summary>
        /// <returns>The independent copy of this context.</returns>
        public DecodingContext Copy() => new DecodingContext(encoding, decoder is not null);

        /// <inheritdoc/>
        object ICloneable.Clone() => Copy();

        /// <summary>
        /// Sets the encapsulated decoder to its initial state.
        /// </summary>
        public void Reset() => decoder?.Reset();

        /// <summary>
        /// Gets encoding associated with this context.
        /// </summary>
        public Encoding Encoding => encoding ?? Encoding.Default;

        internal Decoder GetDecoder() => decoder ?? Encoding.GetDecoder();

        /// <summary>
        /// Creates decoding context.
        /// </summary>
        /// <param name="encoding">The text encoding.</param>
        public static implicit operator DecodingContext(Encoding encoding) => new DecodingContext(encoding, false);
    }
}
