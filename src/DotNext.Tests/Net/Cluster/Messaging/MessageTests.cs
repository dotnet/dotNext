using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Text;

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
            using var content = new MemoryStream();
            await pipe.Reader.CopyToAsync(content);
            content.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(content, Encoding.UTF8, false, 1024, true))
            {
                Equal("Hello, world!", reader.ReadToEnd());
            }
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
            using var content = new MemoryStream();
            await pipe.Reader.CopyToAsync(content);
            content.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(content, Encoding.UTF8, false, 1024, true))
            {
                Equal("abcde", reader.ReadToEnd());
            }
        }

        public sealed class JsonObject
        {
            public string Message { get; set; }

            public int Arg { get; set; }
        }

        [Fact]
        public static async Task JsonMessageSerialization()
        {
            var pipe = new Pipe();
            IMessage message = new JsonMessage<JsonObject>("JsonObj", new JsonObject { Message = "Hello, world!", Arg = 42 });
            ThreadPool.QueueUserWorkItem(async state =>
            {
                await message.WriteToAsync(pipe.Writer).ConfigureAwait(false);
                pipe.Writer.Complete();
            });

            JsonObject obj;
            using (var content = new MemoryStream())
            {
                await pipe.Reader.CopyToAsync(content);
                content.Seek(0, SeekOrigin.Begin);
                MessageReader<JsonObject> reader = JsonMessage<JsonObject>.FromJsonAsync;
                obj = await reader(new StreamMessage(content, true, "JsonObj"), CancellationToken.None);
            }

            Equal("Hello, world!", obj.Message);
            Equal(42, obj.Arg);
        }
    }
}
