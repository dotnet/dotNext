using System.IO.Pipelines;
using System.Text;

namespace DotNext.Net.Cluster.Messaging;

using IO;
using Text.Json;

public sealed class MessageTests : Test
{
    [Fact]
    public static async Task TextMessageUsingStream()
    {
        IMessage message = new TextMessage("Hello, world!", "msg");
        using var content = new MemoryStream(1024);
        await message.WriteToAsync(content);
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
        using var reader = new StreamReader(content, Encoding.UTF8, false, 1024, true);
        Equal("Hello, world!", reader.ReadToEnd());
    }

    [Fact]
    public static async Task JsonMessageSerialization()
    {
        var pipe = new Pipe();
        IMessage message = new JsonMessage<TestJsonObject> { Name = "JsonObj", Content = new() { StringField = "Hello, world!" } };
        ThreadPool.QueueUserWorkItem(async state =>
        {
            await message.WriteToAsync(pipe.Writer).ConfigureAwait(false);
            pipe.Writer.Complete();
        });

        TestJsonObject obj;
        using (var content = new MemoryStream())
        {
            await pipe.Reader.CopyToAsync(content);
            content.Seek(0, SeekOrigin.Begin);
            MessageReader<TestJsonObject> reader = JsonSerializable<TestJsonObject>.TransformAsync;
            obj = await reader(new StreamMessage(content, true, "JsonObj"), CancellationToken.None);
        }

        Equal("Hello, world!", obj.StringField.Value);
    }
}