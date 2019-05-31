using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster
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
        /// Gets length of the message payload.
        /// </summary>
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
}
