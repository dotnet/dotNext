using System.Net.Mime;

namespace DotNext.Net.Cluster.Messaging;

using Collections.Specialized;
using Runtime.Serialization;

/// <summary>
/// Represents typed client for sending messages to the nodes in the cluster.
/// </summary>
public class MessagingClient
{
    private readonly IOutputChannel channel;
    private readonly IMessageRegistry messages;

    /// <summary>
    /// Constructs a new typed client for messaging.
    /// </summary>
    /// <param name="channel">The output channel for outbound messages.</param>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> is <see langword="null"/>.</exception>
    public MessagingClient(IOutputChannel channel)
    {
        this.channel = channel ?? throw new ArgumentNullException(nameof(channel));
        var attributes = (MessageAttribute[])Attribute.GetCustomAttributes(GetType(),
            typeof(MessageAttribute),
            inherit: true);

        messages = attributes is []
            ? new OptimizedMessageRegistry()
            : new DynamicMessageRegistry(attributes);
    }

    /// <summary>
    /// Gets the name of MIME type by message type.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message.</typeparam>
    /// <returns>The message name and MIME type.</returns>
    /// <exception cref="GenericArgumentException{TMessage}"><typeparamref name="TMessage"/> is not registered via <see cref="RegisterMessage{TMessage}(string, ContentType?)"/>.</exception>
    protected virtual (string MessageName, ContentType MessageType) GetMessageInfo<TMessage>()
        where TMessage : ISerializable<TMessage>
        => messages.Get<TMessage>();

    /// <summary>
    /// Registers message type and its type.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message payload.</typeparam>
    /// <param name="name">The name of the message.</param>
    /// <param name="type">MIME type of the message.</param>
    /// <returns>This client.</returns>
    public MessagingClient RegisterMessage<TMessage>(string name, ContentType? type = null)
        where TMessage : ISerializable<TMessage>
    {
        messages.Add<TMessage>(name, type ?? new ContentType(MediaTypeNames.Application.Octet));
        return this;
    }

    /// <summary>
    /// Registers message type and its type.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message payload.</typeparam>
    /// <param name="messageInfo">The information about message type.</param>
    /// <returns>This client.</returns>
    public MessagingClient RegisterMessage<TMessage>(MessageAttribute<TMessage> messageInfo)
        where TMessage : ISerializable<TMessage>
        => RegisterMessage<TMessage>(messageInfo.Name, new(messageInfo.MimeType));

    /// <summary>
    /// Sends a request message.
    /// </summary>
    /// <typeparam name="TInput">The type of the request.</typeparam>
    /// <typeparam name="TOutput">The type of the response.</typeparam>
    /// <param name="input">The request.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The response.</returns>
    /// <exception cref="InvalidOperationException">Attempts to send message to local or unavailable endpoint.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task<TOutput> SendMessageAsync<TInput, TOutput>(TInput input, CancellationToken token = default)
        where TInput : ISerializable<TInput>
        where TOutput : ISerializable<TOutput>
    {
        Task<TOutput> result;
        try
        {
            result = channel.SendMessageAsync<TOutput>(CreateMessage(input), token);
        }
        catch (Exception e)
        {
            result = Task.FromException<TOutput>(e);
        }

        return result;
    }

    /// <summary>
    /// Sends one-way message.
    /// </summary>
    /// <typeparam name="TInput">The type of the message.</typeparam>
    /// <param name="input">The message to send.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="InvalidOperationException">Attempts to send message to local or unavailable endpoint.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task SendSignalAsync<TInput>(TInput input, CancellationToken token = default)
        where TInput : ISerializable<TInput>
    {
        Task result;
        try
        {
            result = channel.SendSignalAsync(CreateMessage(input), token);
        }
        catch (Exception e)
        {
            result = Task.FromException(e);
        }

        return result;
    }

    private Message<TInput> CreateMessage<TInput>(TInput payload)
        where TInput : ISerializable<TInput>
    {
        var (name, type) = GetMessageInfo<TInput>();
        return new() { Name = name, Payload = payload, Type = type };
    }
    
    private interface IMessageRegistry
    {
        (string MessageName, ContentType MessageType) Get<TMessage>()
            where TMessage : ISerializable<TMessage>;

        void Add<TMessage>(string name, ContentType contentType);
    }

    private sealed class OptimizedMessageRegistry : TypeMap<(string MessageName, ContentType MessageType)>, IMessageRegistry
    {
        (string MessageName, ContentType MessageType) IMessageRegistry.Get<TMessage>()
            => TryGetValue<TMessage>(out var entry)
                ? entry
                : throw new GenericArgumentException<TMessage>(ExceptionMessages.MissingMessageName);

        void IMessageRegistry.Add<TMessage>(string name, ContentType contentType)
            => Add<TMessage>((name, contentType));
    }

    private sealed class DynamicMessageRegistry(IEnumerable<MessageAttribute> attributes)
        : Dictionary<Type, (string MessageName, ContentType MessageType)>(attributes.Select(ToKeyValuePar)), IMessageRegistry
    {
        private static KeyValuePair<Type, (string MessageName, ContentType MessageType)> ToKeyValuePar(MessageAttribute attribute)
            => new(attribute.MessageType, (attribute.Name, new ContentType(attribute.MimeType)));
        
        (string MessageName, ContentType MessageType) IMessageRegistry.Get<TMessage>()
            => TryGetValue(typeof(TMessage), out var entry)
                ? entry
                : throw new GenericArgumentException<TMessage>(ExceptionMessages.MissingMessageName);

        void IMessageRegistry.Add<TMessage>(string name, ContentType contentType)
            => Add(typeof(TMessage), (name, contentType));
    }
}