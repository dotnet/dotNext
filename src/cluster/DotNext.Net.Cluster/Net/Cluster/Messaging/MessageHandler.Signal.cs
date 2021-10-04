namespace DotNext.Net.Cluster.Messaging;

using Runtime.Serialization;

public partial class MessageHandler
{
    private delegate Task SignalHandler(ISubscriber sender, IMessage signal, object? context, CancellationToken token);

    private sealed class SignalHandler<TInput> : ISupplier<SignalHandler>
        where TInput : notnull, ISerializable<TInput>
    {
        private readonly MulticastDelegate handler;

        public SignalHandler(Func<TInput, CancellationToken, Task> handler)
            => this.handler = handler;

        public SignalHandler(Func<ISubscriber, TInput, CancellationToken, Task> handler)
            => this.handler = handler;

        public SignalHandler(Func<TInput, object?, CancellationToken, Task> handler)
            => this.handler = handler;

        public SignalHandler(Func<ISubscriber, TInput, object?, CancellationToken, Task> handler)
            => this.handler = handler;

        private Task HandleAsync(ISubscriber sender, TInput signal, object? context, CancellationToken token) => handler switch
        {
            Func<TInput, CancellationToken, Task> h => h.Invoke(signal, token),
            Func<ISubscriber, TInput, CancellationToken, Task> h => h.Invoke(sender, signal, token),
            Func<TInput, object?, CancellationToken, Task> h => h.Invoke(signal, context, token),
            Func<ISubscriber, TInput, object?, CancellationToken, Task> h => h.Invoke(sender, signal, context, token),
            _ => Task.FromException(new NotImplementedException()),
        };

        private async Task HandleAsync(ISubscriber sender, IMessage signal, object? context, CancellationToken token)
        {
            var input = await signal.TransformAsync<IMessage, TInput>(token).ConfigureAwait(false);
            await HandleAsync(sender, input, context, token).ConfigureAwait(false);
        }

        SignalHandler ISupplier<SignalHandler>.Invoke() => HandleAsync;

        public static implicit operator SignalHandler(SignalHandler<TInput> handler)
            => handler.HandleAsync;
    }
}