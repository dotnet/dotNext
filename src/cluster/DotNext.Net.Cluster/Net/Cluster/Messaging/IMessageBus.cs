using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Specifies a cloud of nodes that can communicate with each other through the network.
    /// </summary>
    public interface IMessageBus : ICluster
    {
        /// <summary>
        /// Gets the leader node.
        /// </summary>
        new ISubscriber? Leader { get; }

        IClusterMember? ICluster.Leader => Leader;

        /// <summary>
        /// Represents a collection of nodes in the network.
        /// </summary>
        new IReadOnlyCollection<ISubscriber> Members { get; }

        IReadOnlyCollection<IClusterMember> ICluster.Members => Members;

        /// <summary>
        /// Sends a message to the cluster leader.
        /// </summary>
        /// <remarks>
        /// <paramref name="message"/> should be reusable because <see cref="IO.IDataTransferObject.TransformAsync{TWriter}"/> 
        /// can be called multiple times.
        /// </remarks>
        /// <typeparam name="TResponse">The type of the parsed response message.</typeparam>
        /// <param name="message">The message to be sent.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <param name="responseReader">The response reader.</param>
        /// <returns>The message representing response; or <see langword="null"/> if request message in one-way.</returns>
        /// <exception cref="InvalidOperationException">Leader node is not present in the cluster.</exception>
        Task<TResponse> SendMessageToLeaderAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, CancellationToken token = default);

        /// <summary>
        /// Sends one-way message to the cluster leader.
        /// </summary>
        /// <remarks>
        /// <paramref name="message"/> should be reusable because <see cref="IO.IDataTransferObject.TransformAsync{TWriter}"/> 
        /// can be called multiple times.
        /// </remarks>
        /// <param name="message">The message to be sent.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The task representing execution of this method.</returns>
        /// <exception cref="InvalidOperationException">Leader node is not present in the cluster.</exception>
        Task SendSignalToLeaderAsync(IMessage message, CancellationToken token = default);
    }
}