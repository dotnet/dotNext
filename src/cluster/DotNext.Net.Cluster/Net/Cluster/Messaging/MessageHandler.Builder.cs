using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    public partial class MessageHandler
    {
        /// <summary>
        /// Represents builder of message handlers.
        /// </summary>
        public sealed class Builder : ISupplier<MessageHandler>
        {
            private readonly Dictionary<string, RpcHandler> rpcHandlers;
            private readonly Dictionary<string, SignalHandler> signalHandlers;

            /// <summary>
            /// Initializes a new builder.
            /// </summary>
            public Builder()
            {
                rpcHandlers = new(StringComparer.Ordinal);
                signalHandlers = new(StringComparer.Ordinal);
            }

            /// <summary>
            /// Registers signal handler.
            /// </summary>
            /// <typeparam name="TInput">The type of the message.</typeparam>
            /// <param name="signalHandler">The handler.</param>
            /// <returns>This builder.</returns>
            public Builder Add<TInput>(Func<TInput, CancellationToken, Task> signalHandler)
            {
                var handler = new SignalHandler<TInput>(signalHandler, out var messageName);
                signalHandlers.Add(messageName, handler);
                return this;
            }

            /// <summary>
            /// Registers signal handler.
            /// </summary>
            /// <typeparam name="TInput">The type of the message.</typeparam>
            /// <param name="signalHandler">The handler.</param>
            /// <returns>This builder.</returns>
            public Builder Add<TInput>(Func<TInput, object?, CancellationToken, Task> signalHandler)
            {
                var handler = new SignalHandler<TInput>(signalHandler, out var messageName);
                signalHandlers.Add(messageName, handler);
                return this;
            }

            /// <summary>
            /// Registers signal handler.
            /// </summary>
            /// <typeparam name="TInput">The type of the message.</typeparam>
            /// <param name="signalHandler">The handler.</param>
            /// <returns>This builder.</returns>
            public Builder Add<TInput>(Func<ISubscriber, TInput, CancellationToken, Task> signalHandler)
            {
                var handler = new SignalHandler<TInput>(signalHandler, out var messageName);
                signalHandlers.Add(messageName, handler);
                return this;
            }

            /// <summary>
            /// Registers signal handler.
            /// </summary>
            /// <typeparam name="TInput">The type of the message.</typeparam>
            /// <param name="signalHandler">The handler.</param>
            /// <returns>This builder.</returns>
            public Builder Add<TInput>(Func<ISubscriber, TInput, object?, CancellationToken, Task> signalHandler)
            {
                var handler = new SignalHandler<TInput>(signalHandler, out var messageName);
                signalHandlers.Add(messageName, handler);
                return this;
            }

            /// <summary>
            /// Registers duplex message handler.
            /// </summary>
            /// <typeparam name="TInput">The type of the input message.</typeparam>
            /// <typeparam name="TOutput">The type of the output message.</typeparam>
            /// <param name="messageHandler">The message handler.</param>
            /// <returns>This builder.</returns>
            public Builder Add<TInput, TOutput>(Func<TInput, CancellationToken, Task<TOutput>> messageHandler)
            {
                var handler = new RpcHandler<TInput, TOutput>(messageHandler, out var inputMessageName);
                rpcHandlers.Add(inputMessageName, handler);
                return this;
            }

            /// <summary>
            /// Registers duplex message handler.
            /// </summary>
            /// <typeparam name="TInput">The type of the input message.</typeparam>
            /// <typeparam name="TOutput">The type of the output message.</typeparam>
            /// <param name="messageHandler">The message handler.</param>
            /// <returns>This builder.</returns>
            public Builder Add<TInput, TOutput>(Func<TInput, object?, CancellationToken, Task<TOutput>> messageHandler)
            {
                var handler = new RpcHandler<TInput, TOutput>(messageHandler, out var inputMessageName);
                rpcHandlers.Add(inputMessageName, handler);
                return this;
            }

            /// <summary>
            /// Registers duplex message handler.
            /// </summary>
            /// <typeparam name="TInput">The type of the input message.</typeparam>
            /// <typeparam name="TOutput">The type of the output message.</typeparam>
            /// <param name="messageHandler">The message handler.</param>
            /// <returns>This builder.</returns>
            public Builder Add<TInput, TOutput>(Func<ISubscriber, TInput, CancellationToken, Task<TOutput>> messageHandler)
            {
                var handler = new RpcHandler<TInput, TOutput>(messageHandler, out var inputMessageName);
                rpcHandlers.Add(inputMessageName, handler);
                return this;
            }

            /// <summary>
            /// Registers duplex message handler.
            /// </summary>
            /// <typeparam name="TInput">The type of the input message.</typeparam>
            /// <typeparam name="TOutput">The type of the output message.</typeparam>
            /// <param name="messageHandler">The message handler.</param>
            /// <returns>This builder.</returns>
            public Builder Add<TInput, TOutput>(Func<ISubscriber, TInput, object?, CancellationToken, Task<TOutput>> messageHandler)
            {
                var handler = new RpcHandler<TInput, TOutput>(messageHandler, out var inputMessageName);
                rpcHandlers.Add(inputMessageName, handler);
                return this;
            }

            /// <summary>
            /// Clears this builder so it can be reused.
            /// </summary>
            public void Clear()
            {
                rpcHandlers.Clear();
                signalHandlers.Clear();
            }

            /// <summary>
            /// Constructs message hander based on registered delegates.
            /// </summary>
            /// <returns>The constructed message handler.</returns>
            public MessageHandler Build() => new(rpcHandlers, signalHandlers);

            /// <inheritdoc/>
            MessageHandler ISupplier<MessageHandler>.Invoke() => Build();
        }
    }
}