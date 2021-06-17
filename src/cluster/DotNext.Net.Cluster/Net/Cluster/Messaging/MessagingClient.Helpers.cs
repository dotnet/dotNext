using System.Net.Mime;

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
            private readonly string messageName;
            private readonly ContentType messageType;

            internal InputMessageFactory()
            {
                formatter = MessageAttribute.GetFormatter<T>(out messageName, out messageType);
            }

            internal override string MessageName => messageName;

            internal override ContentType MessageType => messageType;

            internal Message<T> CreateMessage(T payload) => new(MessageName, payload, formatter, MessageType);
        }

        private static MessageReader<TOutput> CreateReader<TOutput>()
            => MessageReader.CreateReader(MessageAttribute.GetFormatter<TOutput>(out _));
    }
}