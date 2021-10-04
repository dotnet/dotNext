namespace DotNext.Net.Cluster.Messaging;

using Runtime.Serialization;

public partial class MessageHandler
{
    private delegate Task<IMessage> RpcHandler(ISubscriber sender, IMessage request, object? context, CancellationToken token);

    private sealed class RpcHandler<TInput, TOutput> : ISupplier<RpcHandler>
        where TInput : notnull, ISerializable<TInput>
        where TOutput : notnull, ISerializable<TOutput>
    {
        private readonly string outputMessageName;
        private readonly string? outputMessageType;
        private readonly MulticastDelegate handler;

        public RpcHandler(Func<TInput, CancellationToken, Task<TOutput>> handler, string outputMessageName, string? outputMessageType)
        {
            this.handler = handler;
            this.outputMessageName = outputMessageName;
            this.outputMessageType = outputMessageType;
        }

        public RpcHandler(Func<ISubscriber, TInput, CancellationToken, Task<TOutput>> handler, string outputMessageName, string? outputMessageType)
        {
            this.handler = handler;
            this.outputMessageName = outputMessageName;
            this.outputMessageType = outputMessageType;
        }

        public RpcHandler(Func<TInput, object?, CancellationToken, Task<TOutput>> handler, string outputMessageName, string? outputMessageType)
        {
            this.handler = handler;
            this.outputMessageName = outputMessageName;
            this.outputMessageType = outputMessageType;
        }

        public RpcHandler(Func<ISubscriber, TInput, object?, CancellationToken, Task<TOutput>> handler, string outputMessageName, string? outputMessageType)
        {
            this.handler = handler;
            this.outputMessageName = outputMessageName;
            this.outputMessageType = outputMessageType;
        }

        private Task<TOutput> HandleAsync(ISubscriber sender, TInput input, object? context, CancellationToken token) => handler switch
        {
            Func<TInput, CancellationToken, Task<TOutput>> h => h.Invoke(input, token),
            Func<ISubscriber, TInput, CancellationToken, Task<TOutput>> h => h.Invoke(sender, input, token),
            Func<TInput, object?, CancellationToken, Task<TOutput>> h => h.Invoke(input, context, token),
            Func<ISubscriber, TInput, object?, CancellationToken, Task<TOutput>> h => h.Invoke(sender, input, context, token),
            _ => Task.FromException<TOutput>(new NotImplementedException()),
        };

        private async Task<IMessage> HandleAsync(ISubscriber sender, IMessage request, object? context, CancellationToken token)
        {
            var input = await request.TransformAsync<IMessage, TInput>(token).ConfigureAwait(false);
            var output = await HandleAsync(sender, input, context, token).ConfigureAwait(false);
            return new Message<TOutput>(outputMessageName, output, outputMessageType);
        }

        RpcHandler ISupplier<RpcHandler>.Invoke() => HandleAsync;

        public static implicit operator RpcHandler(RpcHandler<TInput, TOutput> handler)
            => handler.HandleAsync;
    }
}