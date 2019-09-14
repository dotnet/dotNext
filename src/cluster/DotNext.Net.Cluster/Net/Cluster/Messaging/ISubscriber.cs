using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Represents cluster member that supports messaging.
    /// </summary>
    public interface ISubscriber : IClusterMember
    {
        /// <summary>
        /// Sends a message to the cluster member.
        /// </summary>
        /// <remarks>
        /// The message content may be available inside of <paramref name="responseReader"/> only.
        /// Do not try to return <see cref="IMessage">response message</see> itself from the delegate.
        /// </remarks>
        /// <typeparam name="TResponse">The type of the parsed response message.</typeparam>
        /// <param name="message">The message to be sent.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <param name="responseReader">The response reader.</param>
        /// <returns>The message representing response; or <see langword="null"/> if request message in one-way.</returns>
        /// <exception cref="InvalidOperationException">Attempts to send message to local or unavailable member.</exception>
        Task<TResponse> SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, CancellationToken token = default);

        /// <summary>
        /// Sends one-way message to the cluster member.
        /// </summary>
        /// <param name="message">The message to be sent.</param>
        /// <param name="requiresConfirmation"><see langword="true"/> to wait for confirmation of delivery from receiver; otherwise, <see langword="false"/>.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The task representing execution of this method.</returns>
        /// <exception cref="InvalidOperationException">Attempts to send message to local or unavailable member.</exception>
        Task SendSignalAsync(IMessage message, bool requiresConfirmation = true, CancellationToken token = default);
    }
}