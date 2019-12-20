using DotNext.Net.Mime;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Represents helper methods allow to communicate with remove cluster members
    /// through network.
    /// </summary>
    public static class Messenger
    {
        /// <summary>
        /// Send broadcast one-way message to all members in the cluster except local member.
        /// </summary>
        /// <param name="cluster">The cluster of nodes.</param>
        /// <param name="message">The message to be sent.</param>
        /// <param name="requiresConfirmation"><see langword="true"/> to wait for confirmation of delivery from receiver; otherwise, <see langword="false"/>.</param>
        /// <returns>The task representing asynchronous execution of broadcasting.</returns>
        public static Task SendBroadcastSignalAsync(this IMessageBus cluster, IMessage message, bool requiresConfirmation = true)
        {
            ICollection<Task> tasks = new LinkedList<Task>();
            foreach (var member in cluster.Members)
                if (member.IsRemote)
                    tasks.Add(member.SendSignalAsync(message, requiresConfirmation));
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Send synchronous text message.
        /// </summary>
        /// <typeparam name="TResponse">The type of the parsed response message.</typeparam>
        /// <param name="messenger">The receiver of the message.</param>
        /// <param name="responseReader">The response reader.</param>
        /// <param name="messageName">The name of the message.</param>
        /// <param name="text">The content of the message.</param>
        /// <param name="mediaType">The media type of the message content.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The reply message.</returns>
        public static Task<TResponse> SendTextMessageAsync<TResponse>(this ISubscriber messenger, MessageReader<TResponse> responseReader, string messageName, string text, string mediaType = null, CancellationToken token = default)
            => messenger.SendMessageAsync(new TextMessage(messageName, text, mediaType), responseReader, token);

        /// <summary>
        /// Send one-way text message.
        /// </summary>
        /// <param name="messenger">The receiver of the message.</param>
        /// <param name="messageName">The name of the message.</param>
        /// <param name="text">The content of the message.</param>
        /// <param name="mediaType">The media type of the message content.</param>
        /// <param name="requiresConfirmation"><see langword="true"/> to wait for confirmation of delivery from receiver; otherwise, <see langword="false"/>.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The task representing asynchronous execution of the method.</returns>
        public static Task SendTextSignalAsync(this ISubscriber messenger, string messageName, string text, bool requiresConfirmation = true, string mediaType = null, CancellationToken token = default)
            => messenger.SendSignalAsync(new TextMessage(messageName, text, mediaType), requiresConfirmation, token);

        /// <summary>
        /// Converts message content into string.
        /// </summary>
        /// <param name="message">The message to read.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The content of the message.</returns>
        public static Task<string> ReadAsTextAsync(this IMessage message, CancellationToken token = default)
            => message is TextMessage text ? Task.FromResult(text.Content) : DataTransferObject.ReadAsTextAsync(message, message.Type.GetEncoding(), token);
    }
}