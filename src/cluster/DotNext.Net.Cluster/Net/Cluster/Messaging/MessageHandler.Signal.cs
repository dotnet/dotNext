using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Messaging;

using Runtime.Serialization;

public partial class MessageHandler
{
    private delegate Task SignalHandler(ISubscriber sender, IMessage signal, object? context, CancellationToken token);

    [StructLayout(LayoutKind.Auto)]
    private readonly struct SignalHandler<TInput>(Func<ISubscriber, TInput, object?, CancellationToken, Task> handler) : ISupplier<SignalHandler>
        where TInput : ISerializable<TInput>
    {
        private readonly SignalHandler handler = handler.Invoke;

        public SignalHandler(Func<TInput, CancellationToken, Task> handler)
            : this(new Func<ISubscriber, TInput, object?, CancellationToken, Task>(handler.Invoke))
        {
        }

        public SignalHandler(Func<ISubscriber, TInput, CancellationToken, Task> handler)
            : this(new Func<ISubscriber, TInput, object?, CancellationToken, Task>(handler.Invoke))
        {
        }

        public SignalHandler(Func<TInput, object?, CancellationToken, Task> handler)
            : this(new Func<ISubscriber, TInput, object?, CancellationToken, Task>(handler.Invoke))
        {
        }

        SignalHandler ISupplier<SignalHandler>.Invoke() => handler;

        public static implicit operator SignalHandler(SignalHandler<TInput> handler)
            => handler.handler;
    }
}

file static class HandlerBridge
{
    public static Task Invoke<TMessage>(this Func<TMessage, CancellationToken, Task> handler,
        ISubscriber sender,
        TMessage message,
        object? context,
        CancellationToken token)
        where TMessage : ISerializable<TMessage>
        => handler.Invoke(message, token);

    public static Task Invoke<TMessage>(this Func<ISubscriber, TMessage, CancellationToken, Task> handler,
        ISubscriber sender,
        TMessage message,
        object? context,
        CancellationToken token)
        where TMessage : ISerializable<TMessage>
        => handler.Invoke(sender, message, token);

    public static Task Invoke<TMessage>(this Func<TMessage, object?, CancellationToken, Task> handler,
        ISubscriber sender,
        TMessage message,
        object? context,
        CancellationToken token)
        where TMessage : ISerializable<TMessage>
        => handler.Invoke(message, context, token);

    public static async Task Invoke<TMessage>(this Func<ISubscriber, TMessage, object?, CancellationToken, Task> handler,
        ISubscriber sender,
        IMessage signal,
        object? context,
        CancellationToken token)
        where TMessage : ISerializable<TMessage>
    {
        var input = await ISerializable<TMessage>.TransformAsync(signal, token).ConfigureAwait(false);
        await handler.Invoke(sender, input, context, token).ConfigureAwait(false);
    }
}