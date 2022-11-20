using System.Buffers;
using System.Diagnostics.CodeAnalysis;

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

        [Fact]
        public static void ReadFromStart()
        {
            using var writer = new SparseBufferWriter<int>(chunkSize: 16);
            var current = writer.End;
            writer.Write(Enumerable.Range(0, 16).ToArray());

            Collection(writer.Read(current, 16), static block => Equal(Enumerable.Range(0, 16).ToArray(), block.ToArray()));
        }

        [Fact]
        public static void ReadLastElements()
        {
            using var writer = new SparseBufferWriter<int>(chunkSize: 16);
            writer.Write(new int[3]);

            var current = writer.End;
            writer.Write(Enumerable.Range(0, 16).ToArray());
            writer.Write(Enumerable.Range(16, 16).ToArray());

            Collection(
                writer.Read(current, 32),
                static block => Equal(Enumerable.Range(0, 13).ToArray(), block.ToArray()),
                static block => Equal(Enumerable.Range(13, 16).ToArray(), block.ToArray()),
                static block => Equal(Enumerable.Range(29, 3).ToArray(), block.ToArray()));
        }

        [Fact]
        public static void CopyChunks()
        {
            using var writer = new SparseBufferWriter<int>(chunkSize: 16);
            writer.Write(Enumerable.Range(0, 16).ToArray());
            writer.Write(Enumerable.Range(16, 16).ToArray());

            var position = default(SequencePosition);
            var buffer = new int[16];
            Equal(buffer.Length, writer.CopyTo(buffer, ref position));
            Equal(Enumerable.Range(0, 16).ToArray(), buffer);

            Equal(buffer.Length, writer.CopyTo(buffer, ref position));
            Equal(Enumerable.Range(16, 16).ToArray(), buffer);

            Equal(0, writer.CopyTo(buffer, ref position));
        }

        [Fact]
        public static void CopyChunksToStream()
        {
            using var writer = new SparseBufferWriter<byte>(chunkSize: 16);
            writer.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 });

            var middle = writer.End;
            writer.Write(new byte[] { 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 });

            using var dest = new MemoryStream(capacity: 32);
            writer.CopyTo<IO.StreamConsumer>(dest, default(SequencePosition));
            Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 }, dest.ToArray());

            dest.Position = 0L;
            dest.SetLength(0L);
            Equal(2L, writer.CopyTo<IO.StreamConsumer>(dest, ref middle, 2L));
            Equal(new byte[] { 16, 17 }, dest.ToArray());
        }

        [Fact]
        public static void EnumerateSegments()
        {
            using var writer = new SparseBufferWriter<int>(chunkSize: 16);
            for (var i = 0; i < 32; i++)
                writer.Add(i);

            Collection(
                writer,
                static block => Equal(Enumerable.Range(0, 16).ToArray(), block.ToArray()),
                static block => Equal(Enumerable.Range(16, 16), block.ToArray()));
        }
    }
}