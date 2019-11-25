using System.IO.Pipelines;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.IO.Pipelines
{
    public sealed class PipeExtensionsTests : Assert
    {
        [Fact]
        public static async Task EncodeDecodeValue()
        {
            static async void WriteValueAsync(decimal value, PipeWriter writer)
            {
                await writer.WriteAsync(value);
            }

            var pipe = new Pipe();
            WriteValueAsync(20M, pipe.Writer);
            Equal(10M, await pipe.Reader.ReadAsync<decimal>());
        }
    }
}
