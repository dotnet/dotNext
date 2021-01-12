using System.Net.Mime;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Represents message that can be transferred between cluster nodes.
    /// </summary>
    /// <remarks>
    /// Message is a low-level abstraction representing logical protocol-independent transport unit used for communication between nodes.
    /// This interface should not be implemented by entities at higher level of abstraction such as Business Layer. It is similar
    /// to Data Transfer Object.
    /// </remarks>
    /// <seealso cref="TextMessage"/>
    /// <seealso cref="BinaryMessage"/>
    /// <seealso cref="StreamMessage"/>
    public interface IMessage : IO.IDataTransferObject
    {
        /// <summary>
        /// Gets name of the message.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// MIME type of the message.
        /// </summary>
        ContentType Type { get; }
    }
}
