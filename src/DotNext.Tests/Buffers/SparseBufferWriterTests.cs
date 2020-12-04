using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class SparseBufferWriterTests : Test
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteSequence(bool copyMemory)
        {
            using var writer = new SparseBufferWriter<byte>();
            var sequence = ToReadOnlySequence(new ReadOnlyMemory<byte>(RandomBytes(5000)), 1000);
            writer.Write(in sequence, copyMemory);
            Equal(sequence.ToArray(), writer.ToReadOnlySequence().ToArray());
        }
    }
}