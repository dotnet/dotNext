using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Buffers
{
    using static IO.StreamSource;

    [ExcludeFromCodeCoverage]
    public sealed class SparseBufferWriterTests : Test
    {
        [Theory]
        [InlineData(false, SparseBufferGrowth.None)]
        [InlineData(true, SparseBufferGrowth.None)]
        [InlineData(false, SparseBufferGrowth.Linear)]
        [InlineData(true, SparseBufferGrowth.Linear)]
        [InlineData(false, SparseBufferGrowth.Exponential)]
        [InlineData(true, SparseBufferGrowth.Exponential)]
        public static void WriteSequence(bool copyMemory, SparseBufferGrowth growth)
        {
            using var writer = new SparseBufferWriter<byte>(128, growth);
            var sequence = ToReadOnlySequence(new ReadOnlyMemory<byte>(RandomBytes(5000)), 1000);
            writer.Write(in sequence, copyMemory);
            Equal(sequence.ToArray(), writer.ToReadOnlySequence().ToArray());
        }

        [Theory]
        [InlineData(1000)]
        [InlineData(10_000)]
        [InlineData(100_000)]
        public static void StressTest(int totalSize)
        {
            using var writer = new SparseBufferWriter<byte>();
            using var output = writer.AsStream();
            var data = RandomBytes(2048);
            for (int remaining = totalSize, take; remaining > 0; remaining -= take)
            {
                take = Math.Min(remaining, data.Length);
                output.Write(data, 0, take);
                remaining -= take;
            }
        }

        [Fact]
        public static void ExtractSingleSegment()
        {
            using var writer = new SparseBufferWriter<int>();
            True(writer.IsSingleSegment);
            True(writer.TryGetWrittenContent(out var segment));
            True(segment.IsEmpty);

            writer.Write(10);
            True(writer.IsSingleSegment);
            True(writer.TryGetWrittenContent(out segment));
            Equal(10, segment.Span[0]);
        }
    }
}