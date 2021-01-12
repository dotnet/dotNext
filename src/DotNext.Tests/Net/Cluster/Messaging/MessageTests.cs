using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Messaging
{
    using IO;

    [ExcludeFromCodeCoverage]
    public sealed class MessageTests : Test
    {
        [Fact]
        public static async Task TextMessageUsingStream()
        {
            IMessage message = new TextMessage("Hello, world!", "msg");
            using var content = new MemoryStream(1024);
            await message.WriteToAsync(content).ConfigureAwait(false);
            content.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(content, Encoding.UTF8, false, 1024, true);
            Equal("Hello, world!", reader.ReadToEnd());
        }

        [Fact]
        public static async Task TextMessageUsingPipeline()
        {
            var pipe = new Pipe();
            IMessage message = new TextMessage("Hello, world!", "msg");
            ThreadPool.QueueUserWorkItem(async state =>
            {
                await message.WriteToAsync(pipe.Writer).ConfigureAwait(false);
                pipe.Writer.Complete();
            });
            var content = new MemoryStream();
            while (true)
            {
                var read = await pipe.Reader.ReadAsync().ConfigureAwait(false);
                foreach (var chunk in read.Buffer)
                    await content.WriteAsync(chunk).ConfigureAwait(false);
                pipe.Reader.AdvanceTo(read.Buffer.End);
                if (read.IsCompleted)
                    break;
            }
            content.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(content, Encoding.UTF8, false, 1024, true))
            {
                Equal("Hello, world!", reader.ReadToEnd());
            }
            content.Dispose();
        }

        [Fact]
        public static async Task BinaryMessagePipeline()
        {
            var pipe = new Pipe();
            var bytes = Encoding.UTF8.GetBytes("abcde");
            IMessage message = new BinaryMessage(ToReadOnlySequence<byte>(bytes, 2), "msg");
            ThreadPool.QueueUserWorkItem(async state =>
            {
                await message.WriteToAsync(pipe.Writer).ConfigureAwait(false);
                pipe.Writer.Complete();
            });
            var content = new MemoryStream();
            while (true)
            {
                var read = await pipe.Reader.ReadAsync().ConfigureAwait(false);
                foreach (var chunk in read.Buffer)
                    await content.WriteAsync(chunk).ConfigureAwait(false);
                pipe.Reader.AdvanceTo(read.Buffer.End);
                if (read.IsCompleted)
                    break;
            }
            content.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(content, Encoding.UTF8, false, 1024, true))
            {
                Equal("abcde", reader.ReadToEnd());
            }
            content.Dispose();
        }
    }
}
