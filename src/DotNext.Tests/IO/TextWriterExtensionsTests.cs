using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.IO
{
    public sealed class TextWriterExtensionsTests : Test
    {
        [Fact]
        public static async Task WriteSequence()
        {
            const string value = "abcdefg";
            var sequence = value.Split(3).ToReadOnlySequence();
            await using var writer = new StringWriter();
            await writer.WriteAsync(sequence);
            Equal(value, writer.ToString());
        }
    }
}