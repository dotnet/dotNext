using System.Diagnostics.CodeAnalysis;
using System.IO;
using Xunit;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class SpanReaderTests : Test
    {
        [Fact]
        public static void ReadWrite()
        {
            var writer = new SpanWriter<byte>(stackalloc byte[3]);
            var expected = new byte[] { 10, 20, 30 };
            writer.Write(expected);

            var reader = new SpanReader<byte>(writer.Span);
            Equal(3, reader.RemainingCount);
            Equal(0, reader.ConsumedCount);

            Equal(10, reader.Read());
            Equal(2, reader.RemainingCount);
            Equal(1, reader.ConsumedCount);

            Equal(new byte[] { 20, 30 }, reader.Read(2).ToArray());

            var exceptionThrown = false;
            try
            {
                reader.Read(new byte[2]);
            }
            catch (EndOfStreamException)
            {
                exceptionThrown = true;
            }

            True(exceptionThrown);

            reader.Reset();
            Equal(0, reader.ConsumedCount);
            
            var actual = new byte[3];
            reader.Read(actual);
            Equal(expected, actual);
        }
    }
}