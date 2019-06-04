using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Represents message that can be transferred between cluster nodes.
    /// </summary>
    public interface IMessage
    {
        /// <summary>
        /// Gets name of the message.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets length of the message payload, in bytes.
        /// </summary>
        /// <remarks>
        /// If value is <see langword="null"/> then length of the message cannot be determined.
        /// </remarks>
        long? Length { get; }

        /// <summary>
        /// Copies the message into the specified stream.
        /// </summary>
        /// <param name="output">The output stream receiving message content.</param>
        Task CopyToAsync(Stream output);

        /// <summary>
        /// MIME type of the message.
        /// </summary>
        ContentType Type { get; }
    }

    /// <summary>
    /// Represents typed message.
    /// </summary>
    /// <typeparam name="T">The type of the message content.</typeparam>
    public interface IMessage<T> : IMessage
    {
        /// <summary>
        /// Gets content of this message.
        /// </summary>
        T Content { get; }
    }
}
