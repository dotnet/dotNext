using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Represents asynchronous message reader.
    /// </summary>
    /// <typeparam name="T">The type representing deserialized message content.</typeparam>
    /// <param name="message">The message to be deserialized.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The object representing deserialied message content.</returns>
    public delegate Task<T> MessageReader<T>(IMessage message, CancellationToken token);
}
