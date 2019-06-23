using System;
using System.IO;
using System.IO.Pipelines;
using System.Net.Mime;
using System.Threading;
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
        /// Copies the message content into the specified stream.
        /// </summary>
        /// <param name="output">The output stream receiving message content.</param>
        Task CopyToAsync(Stream output);

        /// <summary>
        /// Copies the message content into the specified pipe writer.
        /// </summary>
        /// <param name="output">The writer.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        ValueTask CopyToAsync(PipeWriter output, CancellationToken token = default);
        
        /// <summary>
        /// MIME type of the message.
        /// </summary>
        ContentType Type { get; }
    }
}
