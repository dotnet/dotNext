using System.Diagnostics.CodeAnalysis;


namespace DotNext.Net.Cluster.Messaging;

[ExcludeFromCodeCoverage]
[Message<AddMessage>(AddMessage.Name)]
[Message<SubtractMessage>(SubtractMessage.Name)]
[Message<ResultMessage>(ResultMessage.Name)]
public class TestMessageHandler : MessageHandler
{
    internal int Result;

    public Task<ResultMessage> AddAsync(AddMessage message, CancellationToken token)
    {
        return Task.FromResult<ResultMessage>(new() { Result = message.Execute() });
    }

    public Task<ResultMessage> SubtractAsync(ISubscriber sender, SubtractMessage message, CancellationToken token)
    {
        return Task.FromResult<ResultMessage>(new() { Result = message.Execute() });
    }

    public Task ReceiveAsync(ResultMessage signal, object context, CancellationToken token)
    {
        Result = signal.Result;
        return Task.CompletedTask;
    }
}