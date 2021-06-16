using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    using Runtime.Serialization;

    /// <summary>
    /// Represents asynchronous message reader.
    /// </summary>
    /// <typeparam name="T">The type representing deserialized message content.</typeparam>
    /// <param name="message">The message to be deserialized.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The object representing deserialied message content.</returns>
    public delegate ValueTask<T> MessageReader<T>(IMessage message, CancellationToken token);

    /// <summary>
    /// Represents various implementations of message readers.
    /// </summary>
    public static class MessageReader
    {
        private static ValueTask<T> DeserializeAsync<T>(this IFormatter<T> formatter, IMessage message, CancellationToken token)
            => message.TransformAsync<T, DeserializingTransformation<T>>(new(formatter), token);

        /// <summary>
        /// Creates message reader based on the specified for type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the message.</typeparam>
        /// <param name="formatter">The formatter that provides deserialization logic.</param>
        /// <returns>The message reader.</returns>
        public static MessageReader<T> CreateReader<T>(IFormatter<T> formatter)
            => formatter.DeserializeAsync<T>;
    }
}
