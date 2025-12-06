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
        await message.WriteToAsync(content, token: TestToken);
        content.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(content, Encoding.UTF8, false, 1024, true);
        Equal("Hello, world!", await reader.ReadToEndAsync(TestToken));
    }

    [Fact]
    public static async Task TextMessageUsingPipeline()
    {
        var pipe = new Pipe();
        IMessage message = new TextMessage("Hello, world!", "msg");
        ThreadPool.QueueUserWorkItem(async _ =>
        {
            await message.WriteToAsync(pipe.Writer).ConfigureAwait(false);
            await pipe.Writer.CompleteAsync();
        });
        using var content = new MemoryStream();
        await pipe.Reader.CopyToAsync(content, TestToken);
        content.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(content, Encoding.UTF8, false, 1024, true);
        Equal("Hello, world!", await reader.ReadToEndAsync(TestToken));
    }

    [Fact]
    public static async Task JsonMessageSerialization()
    {
        var pipe = new Pipe();
        IMessage message = new JsonMessage<TestJsonObject> { Name = "JsonObj", Content = new() { StringField = "Hello, world!" } };
        ThreadPool.QueueUserWorkItem(async _ =>
        {
            await message.WriteToAsync(pipe.Writer).ConfigureAwait(false);
            await pipe.Writer.CompleteAsync();
        });

        TestJsonObject obj;
        using (var content = new MemoryStream())
        {
            await pipe.Reader.CopyToAsync(content, TestToken);
            content.Seek(0, SeekOrigin.Begin);
            MessageReader<TestJsonObject> reader = JsonSerializable<TestJsonObject>.TransformAsync;
            obj = await reader(new StreamMessage(content, true, "JsonObj"), CancellationToken.None);
        }

        Equal("Hello, world!", obj.StringField.Value);
    }
}