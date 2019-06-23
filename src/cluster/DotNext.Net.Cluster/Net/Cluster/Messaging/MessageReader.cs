using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Represents asynchronous message reader.
    /// </summary>
    /// <typeparam name="T">The type representing deserialized message content.</typeparam>
    /// <param name="message">The message to be deserialized.</param>
    /// <returns>The object representing deserialied message content.</returns>
    public delegate Task<T> MessageReader<T>(IMessage message);
}
