using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.IO
{
    public sealed class StreamSegmentTests : Assert
    {
        [Fact]
        public static void ReadByteSequentially()
        {
            var ms = new MemoryStream(new byte[] { 1, 3, 5, 8, 12 });
            using (var segment = new StreamSegment(ms))
            {
                Equal(0, segment.Position);
                segment.SetRange(0, 2);
                Equal(1, segment.ReadByte());
                Equal(1, segment.Position);

                Equal(3, segment.ReadByte());
                Equal(2, segment.Position);

                Equal(-1, segment.ReadByte());
                Equal(2, segment.Position);
            }
        }

        [Fact]
        public static void ReadRange()
        {
            var ms = new MemoryStream(new byte[] { 1, 3, 5, 8, 12 });
            using (var segment = new StreamSegment(ms))
            {
                segment.SetRange(1L, 2L);
                var buffer = new byte[4];
                Equal(2, segment.Read(buffer, 0, buffer.Length));
                Equal(3, buffer[0]);
                Equal(5, buffer[1]);
                Equal(0, buffer[2]);
                Equal(0, buffer[3]);
                //read from the end of the stream
                Equal(-1, segment.ReadByte());
            }
        }

        [Fact]
        public static async Task ReadRangeAsync()
        {
            var ms = new MemoryStream(new byte[] { 1, 3, 5, 8, 12 });
            using (var segment = new StreamSegment(ms))
            {
                segment.SetRange(1L, 2L);
                var buffer = new byte[4];
                Equal(2, await segment.ReadAsync(buffer, 0, buffer.Length));
                Equal(3, buffer[0]);
                Equal(5, buffer[1]);
                Equal(0, buffer[2]);
                Equal(0, buffer[3]);
                //read from the end of the stream
                Equal(-1, segment.ReadByte());
            }
        }
    }
}
