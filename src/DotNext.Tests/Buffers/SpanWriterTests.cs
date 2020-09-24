using Xunit;

namespace DotNext.Buffers
{
    public sealed class SpanWriterTests : Test
    {
        [Fact]
        public static unsafe void WriteAndGet()
        {
            var writer = new SpanWriter<int>(stackalloc int[10]);
            Equal(0, writer.WrittenCount);
            Equal(10, writer.FreeCapacity);
            
            writer.Write(10);
            Equal(1, writer.WrittenCount);
            Equal(9, writer.FreeCapacity);
            
            var segment = writer.Slide(4);
            segment[0] = 20;
            segment[1] = 30;
            segment[2] = 40;
            segment[3] = 50;
            Equal(5, writer.WrittenCount);
            Equal(5, writer.FreeCapacity);
            Equal(new int[] { 10, 20, 30, 40, 50 }, writer.WrittenSpan.ToArray());

            writer.Reset();
            Equal(0, writer.WrittenCount);
        }
    }
}