using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DotNext.IO
{
    using IReadOnlySpanConsumer = Buffers.IReadOnlySpanConsumer<byte>;

    [StructLayout(LayoutKind.Auto)]
    public readonly struct StreamConsumer : IReadOnlySpanConsumer
    {
        private readonly Stream output;

        public StreamConsumer(Stream output) => this.output = output ?? throw new ArgumentNullException(nameof(output));

        /// <inheritdoc />
        void IReadOnlySpanConsumer.Invoke(ReadOnlySpan<byte> input) => output.Write(input);

        public static implicit operator StreamConsumer(Stream output) => new StreamConsumer(output);
    }
}