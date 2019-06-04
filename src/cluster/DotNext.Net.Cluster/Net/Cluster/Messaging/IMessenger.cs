using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Represents cluster member that supports messaging.
    /// </summary>
    public interface IMessenger : IClusterMember
    {
        /// <summary>
        /// Sends a message to the cluster member.
        /// </summary>
        /// <param name="message">The message to be sent.</param>
        /// <returns>The message representing response; or <see langword="null"/> if request message in one-way.</returns>
        /// <exception cref="InvalidOperationException">Attempts to send message to local or unavailable member.</exception>
        Task<IMessage> SendMessageAsync(IMessage message);

        /// <summary>
        /// Sends one-way message to the cluster member.
        /// </summary>
        /// <param name="message">The message to be sent.</param>
        /// <param name="requiresConfirmation"><see langword="true"/> to wait for confirmation of delivery from receiver; otherwise, <see langword="false"/>.</param>
        /// <returns>The task representing execution of this method.</returns>
        /// <exception cref="InvalidOperationException">Attempts to send message to local or unavailable member.</exception>
        Task SendSignalAsync(IMessage message, bool requiresConfirmation = true);
    }
}