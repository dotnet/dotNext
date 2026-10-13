using System.Net.Mime;

namespace DotNext.Net.Cluster.Messaging;

using Runtime.Serialization;

public sealed class MessagingClientTests : Test
{
    [Fact]
    public static void MessageTypeDiscovery()
    {
        var client = new TestMessagingClient();
        Equal(AddMessage.Name, client.GetMessageInfo<AddMessage>().Name);
        Equal(SubtractMessage.Name, client.GetMessageInfo<SubtractMessage>().Name);
        ThrowsAny<GenericArgumentException>(() => client.GetMessageInfo<ResultMessage>());
    }
}

[Message<AddMessage>(AddMessage.Name, MimeType = MediaTypeNames.Application.Octet)]
[Message<SubtractMessage>(SubtractMessage.Name, MimeType = MediaTypeNames.Application.Octet)]
file sealed class TestMessagingClient() : MessagingClient(new TestOutputChannel())
{
    public new (string Name, ContentType Type) GetMessageInfo<TMessage>()
        where TMessage : ISerializable<TMessage>
        => base.GetMessageInfo<TMessage>();
}

file sealed class TestOutputChannel : IOutputChannel
{
    Task<TResponse> IOutputChannel.SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader,
        CancellationToken token)
        => throw new NotImplementedException();

    Task IOutputChannel.SendSignalAsync(IMessage message, CancellationToken token)
        => throw new NotImplementedException();
}