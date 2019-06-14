using System.Threading.Tasks;
using System.Runtime.ConstrainedExecution;

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
        Task<IMessage> ReceiveMessage(IClusterMember sender, IMessage message);

        /// <summary>
        /// Handles incoming signal from the specified cluster member.
        /// </summary>
        /// <param name="sender">The sender of the message.</param>
        /// <param name="signal">The received message representing signal.</param>
        void ReceiveSignal(IClusterMember sender, IMessage signal);
    }
}