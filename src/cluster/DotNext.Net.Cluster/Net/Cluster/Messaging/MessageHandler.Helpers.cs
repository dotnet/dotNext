using System.Net.Mime;
using System.Reflection;

namespace DotNext.Net.Cluster.Messaging
{
    using Runtime.Serialization;

    public partial class MessageHandler
    {
        private static IFormatter<T> GetFormatter<T>(out string messageName)
        {
            var attr = typeof(T).GetCustomAttribute<MessageAttribute>();
            if (attr is null)
                throw new GenericArgumentException<T>(ExceptionMessages.MissingMessageAttribute<T>());

            messageName = attr.Name;
            return attr.CreateFormatter() as IFormatter<T> ?? throw new GenericArgumentException<T>(ExceptionMessages.MissingMessageFormatter<T>());
        }

        private static IFormatter<T> GetFormatter<T>(out string messageName, out ContentType messageType)
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