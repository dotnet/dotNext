using System;
using System.IO;
using System.IO.Pipelines;
using System.Text;
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
                await writer.CompleteAsync();
            }

            var pipe = new Pipe();
            WriteValueAsync(20M, pipe.Writer);
            Equal(20M, await pipe.Reader.ReadAsync<decimal>());
        }

        [Fact]
        public static async Task EncodeDecodeValue2()
        {
            static async void WriteValueAsync(PipeWriter writer)
            {
                if (BitConverter.IsLittleEndian)
                {
                    await writer.WriteAsync(0L);
                    await writer.WriteAsync(20L);
                }
                else
                {
                    await writer.WriteAsync(20L);
                    await writer.WriteAsync(0L);
                }
                await writer.CompleteAsync();
            }

            var pipe = new Pipe();
            WriteValueAsync(pipe.Writer);
            Equal(20M, await pipe.Reader.ReadAsync<decimal>());
        }

        [Fact]
        public static async Task EndOfStream()
        {
            static async void WriteValueAsync(PipeWriter writer)
            {
                await writer.WriteAsync(0L);
                await writer.CompleteAsync();
            }
            var pipe = new Pipe();
            WriteValueAsync(pipe.Writer);
            await ThrowsAsync<EndOfStreamException>(pipe.Reader.ReadAsync<decimal>().AsTask);
        }

        private static async Task EncodeDecodeStringAsync(Encoding encoding, string value)
        {
            var pipe = new Pipe();
            await pipe.Writer.WriteStringAsync(value.AsMemory(), encoding);
            Equal(value, await pipe.Reader.ReadStringAsync(encoding.GetByteCount(value), encoding));
        }

        [Fact]
        public static async Task EncodeDecodeString()
        {
            const string testString = "abc^$&@^$&@)(_+~";
            await EncodeDecodeStringAsync(Encoding.UTF8, testString);
            await EncodeDecodeStringAsync(Encoding.Unicode, testString);
            await EncodeDecodeStringAsync(Encoding.UTF32, testString);
            await EncodeDecodeStringAsync(Encoding.UTF7, testString);
            await EncodeDecodeStringAsync(Encoding.ASCII, testString);
        }
    }
}
