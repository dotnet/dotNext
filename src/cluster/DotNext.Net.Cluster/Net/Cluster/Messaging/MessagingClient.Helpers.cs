using System.Net.Mime;
using System.Reflection;

namespace DotNext.Net.Cluster.Messaging
{
    using Runtime.Serialization;

    public partial class MessagingClient
    {
        private abstract class InputMessageFactory
        {
            internal abstract string MessageName { get; }

            internal abstract ContentType MessageType { get; }
        }

        private sealed class InputMessageFactory<T> : InputMessageFactory
        {
            private readonly IFormatter<T> formatter;

            internal InputMessageFactory()
            {
                var attr = typeof(T).GetCustomAttribute<MessageAttribute>();
                if (attr is null)
                    throw new GenericArgumentException<T>(ExceptionMessages.MissingMessageAttribute<T>());

                MessageName = attr.Name;

                // cache ContentType object for reuse
                MessageType = new(attr.MimeType);

                formatter = attr.CreateFormatter() as IFormatter<T> ?? throw new GenericArgumentException<T>(ExceptionMessages.MissingMessageFormatter<T>());
            }

            internal override string MessageName { get; }

            internal override ContentType MessageType { get; }

            internal Message<T> CreateMessage(T payload) => new(MessageName, payload, formatter, MessageType);
        }

        private static MessageReader<TOutput> CreateReader<TOutput>()
        {
            var attr = typeof(TOutput).GetCustomAttribute<MessageAttribute>();
            if (attr is null)
                throw new GenericArgumentException<TOutput>(ExceptionMessages.MissingMessageAttribute<TOutput>());

            var formatter = attr.CreateFormatter() as IFormatter<TOutput> ?? throw new GenericArgumentException<TOutput>(ExceptionMessages.MissingMessageFormatter<TOutput>());
            return MessageReader.CreateReader(formatter);
        }
    }
}