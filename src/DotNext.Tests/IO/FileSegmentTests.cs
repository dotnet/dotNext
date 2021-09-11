using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.IO
{
    [ExcludeFromCodeCoverage]
    public sealed class FileSegmentTests : Test
    {
        [Fact]
        public static void ReadSegment()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite);
            var bytes = RandomBytes(128);
            RandomAccess.Write(handle, bytes, 0L);

            var segment = new FileSegment(handle);
            False(segment.IsEmpty);
            Equal(bytes.LongLength, segment.Length);
            Equal(0L, segment.Offset);

            segment = segment.Slice(64);
            Equal(64, segment.Offset);
            Equal(64, segment.Length);

            var buffer = new byte[64];
            Equal(buffer.Length, segment.Read(buffer));

            Equal(bytes.AsSpan().Slice(64).ToArray(), buffer);
        }

        [Fact]
        public static void EmptySegment()
        {
            FileSegment segment = default;
            True(segment.IsEmpty);

            Equal(0L, segment.Offset);
            Equal(0L, segment.Length);
            True(segment.Slice(0L).IsEmpty);
            True(segment.Slice(0L, 0L).IsEmpty);

            Span<byte> bytes = stackalloc byte[8];
            Equal(0, segment.Read(bytes));
        }
    }
}