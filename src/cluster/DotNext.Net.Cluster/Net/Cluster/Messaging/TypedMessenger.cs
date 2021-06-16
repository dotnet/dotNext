using System;
using System.Collections.Concurrent;
using System.Net.Mime;
using System.Reflection;

namespace DotNext.Net.Cluster.Messaging
{
    using Runtime.Serialization;

    /// <summary>
    /// Represents common infrastructure for typed messengers.
    /// </summary>
    public abstract partial class TypedMessenger
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

        private readonly ConcurrentDictionary<Type, InputMessageFactory> inputs;
        private readonly ConcurrentDictionary<Type, MulticastDelegate> outputs;

        private protected TypedMessenger()
        {
            inputs = new();
            outputs = new();
        }

        private static MessageReader<TOutput> CreateReader<TOutput>()
        {
            var attr = typeof(TOutput).GetCustomAttribute<MessageAttribute>();
            if (attr is null)
                throw new GenericArgumentException<TOutput>(ExceptionMessages.MissingMessageAttribute<TOutput>());

            var formatter = attr.CreateFormatter() as IFormatter<TOutput> ?? throw new GenericArgumentException<TOutput>(ExceptionMessages.MissingMessageFormatter<TOutput>());
            return MessageReader.CreateReader(formatter);
        }

        private protected Message<TInput> CreateMessage<TInput>(TInput payload)
        {
            var key = typeof(TInput);

            if (inputs.TryGetValue(key, out var untyped) is false || untyped is not InputMessageFactory<TInput> factory)
            {
                inputs[key] = factory = new();
            }

            return factory.CreateMessage(payload);
        }

        private protected MessageReader<TOutput> GetMessageReader<TOutput>()
        {
            var key = typeof(TOutput);

            if (outputs.TryGetValue(key, out var untyped) is false || untyped is not MessageReader<TOutput> reader)
            {
                outputs[key] = reader = CreateReader<TOutput>();
            }

            return reader;
        }
    }
}