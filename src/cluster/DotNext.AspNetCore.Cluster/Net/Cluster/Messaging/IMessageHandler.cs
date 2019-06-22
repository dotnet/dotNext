using System.Runtime.ConstrainedExecution;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Represents incoming message handler that can be registered in DI container.
    /// </summary>
    public interface IMessageHandler
    {
        /// <summary>
        /// Handles incoming message from the specified cluster member.
        /// </summary>
        /// <remarks>
        /// Implementation of this method should handle every exception inside of it
        /// and prepare response message representing such exception.
        /// </remarks>
        /// <param name="sender">The sender of the message.</param>
        /// <param name="message">The received message.</param>
        /// <returns>The response message.</returns>
        [ReliabilityContract(Consistency.MayCorruptProcess, Cer.Success)]
        ValueTask<IMessage> ReceiveMessage(IAddressee sender, IMessage message);

        /// <summary>
        /// Handles incoming signal from the specified cluster member.
        /// </summary>
        /// <param name="sender">The sender of the message.</param>
        /// <param name="signal">The received message representing signal.</param>
        /// <returns>The task representing asynchronous execution of the method.</returns>
        ValueTask ReceiveSignal(IAddressee sender, IMessage signal);
    }
}