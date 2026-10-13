namespace DotNext.Net.Cluster.Messaging;

using Runtime.Serialization;

public partial class MessageHandler
{
    private delegate Task<IMessage> RpcHandler(ISubscriber sender, IMessage request, object? context, CancellationToken token);

    private sealed class RpcHandler<TInput, TOutput>(
        Func<ISubscriber, TInput, object?, CancellationToken, Task<TOutput>> handler,
        string outputMessageName,
        string? outputMessageType)
        : ISupplier<RpcHandler>
        where TInput : ISerializable<TInput>
        where TOutput : ISerializable<TOutput>
    {
        public RpcHandler(Func<TInput, CancellationToken, Task<TOutput>> handler, string outputMessageName, string? outputMessageType)
            : this(new Func<ISubscriber, TInput, object?, CancellationToken, Task<TOutput>>(handler.Invoke), outputMessageName, outputMessageType)
        {
        }

        public RpcHandler(Func<ISubscriber, TInput, CancellationToken, Task<TOutput>> handler, string outputMessageName, string? outputMessageType)
            : this(new Func<ISubscriber, TInput, object?, CancellationToken, Task<TOutput>>(handler.Invoke), outputMessageName, outputMessageType)
        {
        }

        public RpcHandler(Func<TInput, object?, CancellationToken, Task<TOutput>> handler, string outputMessageName, string? outputMessageType)
            : this(new Func<ISubscriber, TInput, object?, CancellationToken, Task<TOutput>>(handler.Invoke), outputMessageName, outputMessageType)
        {
        }

        private async Task<IMessage> HandleAsync(ISubscriber sender, IMessage request, object? context, CancellationToken token)
        {
            var input = await ISerializable<TInput>.TransformAsync(request, token).ConfigureAwait(false);
            var output = await handler.Invoke(sender, input, context, token).ConfigureAwait(false);
            return new Message<TOutput>(outputMessageType) { Name = outputMessageName, Payload = output };
        }

        RpcHandler ISupplier<RpcHandler>.Invoke() => HandleAsync;

        public static implicit operator RpcHandler(RpcHandler<TInput, TOutput> handler)
            => handler.HandleAsync;
    }
}

file static class HandlerBridge
{
    public static Task<TOutput> Invoke<TInput, TOutput>(this Func<TInput, CancellationToken, Task<TOutput>> handler,
        ISubscriber sender,
        TInput input,
        object? context,
        CancellationToken token)
        where TInput : ISerializable<TInput>
        where TOutput : ISerializable<TOutput>
        => handler.Invoke(input, token);
    
    public static Task<TOutput> Invoke<TInput, TOutput>(this Func<ISubscriber, TInput, CancellationToken, Task<TOutput>> handler,
        ISubscriber sender,
        TInput input,
        object? context,
        CancellationToken token)
        where TInput : ISerializable<TInput>
        where TOutput : ISerializable<TOutput>
        => handler.Invoke(sender, input, token);
    
    public static Task<TOutput> Invoke<TInput, TOutput>(this Func<TInput, object?, CancellationToken, Task<TOutput>> handler,
        ISubscriber sender,
        TInput input,
        object? context,
        CancellationToken token)
        where TInput : ISerializable<TInput>
        where TOutput : ISerializable<TOutput>
        => handler.Invoke(input, context, token);
}