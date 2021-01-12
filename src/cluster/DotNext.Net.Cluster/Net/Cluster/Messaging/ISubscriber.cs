using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Represents cluster member that supports messaging.
    /// </summary>
    public interface ISubscriber : IClusterMember, IOutputChannel
    {
        /// <summary>
        /// Sends one-way message to this cluster member.
        /// </summary>
        /// <param name="message">The message to be sent.</param>
        /// <param name="requiresConfirmation"><see langword="true"/> to wait for confirmation of delivery from receiver; otherwise, <see langword="false"/>.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The task representing execution of this method.</returns>
        /// <exception cref="InvalidOperationException">Attempts to send message to local or unavailable member.</exception>
        Task SendSignalAsync(IMessage message, bool requiresConfirmation = true, CancellationToken token = default);

        /// <inheritdoc/>
        Task IOutputChannel.SendSignalAsync(IMessage message, CancellationToken token)
            => SendSignalAsync(message, true, token);
    }
}