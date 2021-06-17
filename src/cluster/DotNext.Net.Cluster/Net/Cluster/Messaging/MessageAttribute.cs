using System;
using System.Net.Mime;
using System.Reflection;

namespace DotNext.Net.Cluster.Messaging
{
    using Runtime.Serialization;

    /// <summary>
    /// Indicates that the type can be used as message payload.
    /// </summary>
    /// <seealso cref="MessagingClient"/>
    /// <seealso cref="MessageHandler"/>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class MessageAttribute : SerializableAttribute
    {
        private string? mimeType;

        /// <summary>
        /// Initializes a new instance of the attribute.
        /// </summary>
        /// <param name="name">The name of the message.</param>
        public MessageAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Gets the name of the message.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets or sets MIME type of the message.
        /// </summary>
        public string MimeType
        {
            get => mimeType.IfNullOrEmpty(MediaTypeNames.Application.Octet);
            set => mimeType = value;
        }

        internal static IFormatter<T> GetFormatter<T>(out string messageName)
        {
            var attr = typeof(T).GetCustomAttribute<MessageAttribute>();
            if (attr is null)
                throw new GenericArgumentException<T>(ExceptionMessages.MissingMessageAttribute<T>());

            messageName = attr.Name;
            return attr.CreateFormatter() as IFormatter<T> ?? throw new GenericArgumentException<T>(ExceptionMessages.MissingMessageFormatter<T>());
        }

        internal static IFormatter<T> GetFormatter<T>(out string messageName, out ContentType messageType)
        {
            var attr = typeof(T).GetCustomAttribute<MessageAttribute>();
            if (attr is null)
                throw new GenericArgumentException<T>(ExceptionMessages.MissingMessageAttribute<T>());

            messageName = attr.Name;
            messageType = new(attr.MimeType);
            return attr.CreateFormatter() as IFormatter<T> ?? throw new GenericArgumentException<T>(ExceptionMessages.MissingMessageFormatter<T>());
        }
    }
}