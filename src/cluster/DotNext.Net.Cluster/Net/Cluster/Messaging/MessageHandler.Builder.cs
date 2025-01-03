namespace DotNext.Net.Cluster.Messaging;

using Patterns;
using Runtime.Serialization;

public partial class MessageHandler : IBuildable<MessageHandler, MessageHandler.Builder>
{
    private static StringComparer NameComparer => StringComparer.Ordinal;

    /// <summary>
    /// Represents builder of message handlers.
    /// </summary>
    public sealed class Builder : ISupplier<MessageHandler>, IResettable
    {
        private readonly Dictionary<string, RpcHandler> rpcHandlers;
        private readonly Dictionary<string, SignalHandler> signalHandlers;

        /// <summary>
        /// Initializes a new builder.
        /// </summary>
        public Builder()
        {
            rpcHandlers = new(NameComparer);
            signalHandlers = new(NameComparer);
        }

        /// <summary>
        /// Registers signal handler.
        /// </summary>
        /// <typeparam name="TInput">The type of the message.</typeparam>
        /// <param name="messageName">The name of the message.</param>
        /// <param name="signalHandler">The handler.</param>
        /// <returns>This builder.</returns>
        public Builder Add<TInput>(string messageName, Func<TInput, CancellationToken, Task> signalHandler)
            where TInput : ISerializable<TInput>
        {
            signalHandlers.Add(messageName, new SignalHandler<TInput>(signalHandler));
            return this;
        }

        /// <summary>
        /// Registers signal handler.
        /// </summary>
        /// <typeparam name="TInput">The type of the message.</typeparam>
        /// <param name="messageName">The name of the message.</param>
        /// <param name="signalHandler">The handler.</param>
        /// <returns>This builder.</returns>
        public Builder Add<TInput>(string messageName, Func<TInput, object?, CancellationToken, Task> signalHandler)
            where TInput : ISerializable<TInput>
        {
            signalHandlers.Add(messageName, new SignalHandler<TInput>(signalHandler));
            return this;
        }

        /// <summary>
        /// Registers signal handler.
        /// </summary>
        /// <typeparam name="TInput">The type of the message.</typeparam>
        /// <param name="messageName">The name of the message.</param>
        /// <param name="signalHandler">The handler.</param>
        /// <returns>This builder.</returns>
        public Builder Add<TInput>(string messageName, Func<ISubscriber, TInput, CancellationToken, Task> signalHandler)
            where TInput : ISerializable<TInput>
        {
            signalHandlers.Add(messageName, new SignalHandler<TInput>(signalHandler));
            return this;
        }

        /// <summary>
        /// Registers signal handler.
        /// </summary>
        /// <typeparam name="TInput">The type of the message.</typeparam>
        /// <param name="messageName">The name of the message.</param>
        /// <param name="signalHandler">The handler.</param>
        /// <returns>This builder.</returns>
        public Builder Add<TInput>(string messageName, Func<ISubscriber, TInput, object?, CancellationToken, Task> signalHandler)
            where TInput : ISerializable<TInput>
        {
            signalHandlers.Add(messageName, new SignalHandler<TInput>(signalHandler));
            return this;
        }

        /// <summary>
        /// Registers duplex message handler.
        /// </summary>
        /// <typeparam name="TInput">The type of the input message.</typeparam>
        /// <typeparam name="TOutput">The type of the output message.</typeparam>
        /// <param name="inputMessageName">The name of <typeparamref name="TInput"/> message.</param>
        /// <param name="messageHandler">The message handler.</param>
        /// <param name="outputMessageName">The name of <typeparamref name="TOutput"/> message.</param>
        /// <param name="outputMessageType">MIME type of <typeparamref name="TOutput"/> message.</param>
        /// <returns>This builder.</returns>
        public Builder Add<TInput, TOutput>(string inputMessageName, Func<TInput, CancellationToken, Task<TOutput>> messageHandler, string outputMessageName, string? outputMessageType = null)
            where TInput : ISerializable<TInput>
            where TOutput : ISerializable<TOutput>
        {
            rpcHandlers.Add(inputMessageName, new RpcHandler<TInput, TOutput>(messageHandler, outputMessageName, outputMessageType));
            return this;
        }

        /// <summary>
        /// Registers duplex message handler.
        /// </summary>
        /// <typeparam name="TInput">The type of the input message.</typeparam>
        /// <typeparam name="TOutput">The type of the output message.</typeparam>
        /// <param name="inputMessageName">The name of <typeparamref name="TInput"/> message.</param>
        /// <param name="messageHandler">The message handler.</param>
        /// <param name="outputMessageName">The name of <typeparamref name="TOutput"/> message.</param>
        /// <param name="outputMessageType">MIME type of <typeparamref name="TOutput"/> message.</param>
        /// <returns>This builder.</returns>
        public Builder Add<TInput, TOutput>(string inputMessageName, Func<TInput, object?, CancellationToken, Task<TOutput>> messageHandler, string outputMessageName, string? outputMessageType = null)
            where TInput : ISerializable<TInput>
            where TOutput : ISerializable<TOutput>
        {
            rpcHandlers.Add(inputMessageName, new RpcHandler<TInput, TOutput>(messageHandler, outputMessageName, outputMessageType));
            return this;
        }

        /// <summary>
        /// Registers duplex message handler.
        /// </summary>
        /// <typeparam name="TInput">The type of the input message.</typeparam>
        /// <typeparam name="TOutput">The type of the output message.</typeparam>
        /// <param name="inputMessageName">The name of <typeparamref name="TInput"/> message.</param>
        /// <param name="messageHandler">The message handler.</param>
        /// <param name="outputMessageName">The name of <typeparamref name="TOutput"/> message.</param>
        /// <param name="outputMessageType">MIME type of <typeparamref name="TOutput"/> message.</param>
        /// <returns>This builder.</returns>
        public Builder Add<TInput, TOutput>(string inputMessageName, Func<ISubscriber, TInput, CancellationToken, Task<TOutput>> messageHandler, string outputMessageName, string? outputMessageType = null)
            where TInput : ISerializable<TInput>
            where TOutput : ISerializable<TOutput>
        {
            rpcHandlers.Add(inputMessageName, new RpcHandler<TInput, TOutput>(messageHandler, outputMessageName, outputMessageType));
            return this;
        }

        /// <summary>
        /// Registers duplex message handler.
        /// </summary>
        /// <typeparam name="TInput">The type of the input message.</typeparam>
        /// <typeparam name="TOutput">The type of the output message.</typeparam>
        /// <param name="inputMessageName">The name of <typeparamref name="TInput"/> message.</param>
        /// <param name="messageHandler">The message handler.</param>
        /// <param name="outputMessageName">The name of <typeparamref name="TOutput"/> message.</param>
        /// <param name="outputMessageType">MIME type of <typeparamref name="TOutput"/> message.</param>
        /// <returns>This builder.</returns>
        public Builder Add<TInput, TOutput>(string inputMessageName, Func<ISubscriber, TInput, object?, CancellationToken, Task<TOutput>> messageHandler, string outputMessageName, string? outputMessageType = null)
            where TInput : ISerializable<TInput>
            where TOutput : ISerializable<TOutput>
        {
            rpcHandlers.Add(inputMessageName, new RpcHandler<TInput, TOutput>(messageHandler, outputMessageName, outputMessageType));
            return this;
        }

        /// <summary>
        /// Clears this builder so it can be reused.
        /// </summary>
        public void Reset()
        {
            rpcHandlers.Clear();
            signalHandlers.Clear();
        }

        /// <summary>
        /// Constructs message handler based on registered delegates.
        /// </summary>
        /// <returns>The constructed message handler.</returns>
        public MessageHandler Build() => new(rpcHandlers, signalHandlers);

        /// <inheritdoc/>
        MessageHandler ISupplier<MessageHandler>.Invoke() => Build();
    }

    /// <inheritdoc cref="IBuildable{TSelf, TBuilder}.CreateBuilder"/>
    static Builder IBuildable<MessageHandler, Builder>.CreateBuilder() => new();
}