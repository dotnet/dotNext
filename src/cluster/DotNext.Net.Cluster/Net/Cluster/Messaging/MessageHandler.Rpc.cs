using System;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    using Runtime.Serialization;

    public partial class MessageHandler
    {
        private delegate Task<IMessage> RpcHandler(ISubscriber sender, IMessage request, object? context, CancellationToken token);

        private sealed class RpcHandler<TInput, TOutput> : ISupplier<RpcHandler>
        {
            private readonly IFormatter<TInput> inputFormatter;
            private readonly IFormatter<TOutput> outputFormatter;
            private readonly string outputMessageName;
            private readonly ContentType outputMessageType;
            private readonly MulticastDelegate handler;

            private RpcHandler(MulticastDelegate handler, out string inputMessageName)
            {
                // process input type
                inputFormatter = GetFormatter<TInput>(out inputMessageName);

                // process output type
                outputFormatter = GetFormatter<TOutput>(out outputMessageName, out outputMessageType);

                this.handler = handler;
            }

            public RpcHandler(Func<TInput, CancellationToken, Task<TOutput>> handler, out string inputMessageName)
                : this(handler.As<MulticastDelegate>(), out inputMessageName)
            {
            }

            public RpcHandler(Func<ISubscriber, TInput, CancellationToken, Task<TOutput>> handler, out string inputMessageName)
                : this(handler.As<MulticastDelegate>(), out inputMessageName)
            {
            }

            public RpcHandler(Func<TInput, object?, CancellationToken, Task<TOutput>> handler, out string inputMessageName)
                : this(handler.As<MulticastDelegate>(), out inputMessageName)
            {
            }

            public RpcHandler(Func<ISubscriber, TInput, object?, CancellationToken, Task<TOutput>> handler, out string inputMessageName)
                : this(handler.As<MulticastDelegate>(), out inputMessageName)
            {
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
                var input = await request.TransformAsync<TInput, DeserializingTransformation<TInput>>(new(inputFormatter)).ConfigureAwait(false);
                var output = await HandleAsync(sender, input, context, token).ConfigureAwait(false);
                return new Message<TOutput>(outputMessageName, output, outputFormatter, outputMessageType);
            }

            RpcHandler ISupplier<RpcHandler>.Invoke() => HandleAsync;

            public static implicit operator RpcHandler(RpcHandler<TInput, TOutput> handler)
                => handler.HandleAsync;
        }
    }
}