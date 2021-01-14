using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DotNext.Text
{
    using IReadOnlySpanConsumer = Buffers.IReadOnlySpanConsumer<char>;

    [StructLayout(LayoutKind.Auto)]
    public readonly struct TextConsumer : IReadOnlySpanConsumer
    {
        private readonly TextWriter output;

        public TextConsumer(TextWriter output)
            => this.output = output ?? throw new ArgumentNullException(nameof(output));

        /// <inheritdoc />
        void IReadOnlySpanConsumer.Invoke(ReadOnlySpan<char> input) => output.Write(input);

        public static implicit operator TextConsumer(TextWriter output) => new TextConsumer(output);
    }
}