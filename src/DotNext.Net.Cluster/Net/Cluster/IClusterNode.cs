using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Represents cluster node in distributed environment.
    /// </summary>
    public interface IClusterNode : IClusterMember, IDisposable
    {
        /// <summary>
        /// Gets cluster status.
        /// </summary>
        ClusterStatus ClusterStatus { get; }

        /// <summary>
        /// Gets collection of members in the cluster node.
        /// </summary>
        IReadOnlyCollection<IClusterMember> Members { get; }

        /// <summary>
        /// Gets the leader node.
        /// </summary>
        IClusterMember Leader { get; }

        /// <summary>
        /// Represents an event raised when leader has been changed.
        /// </summary>
        event LeaderChangedEventHandler LeaderChanged;

        /// <summary>
        /// Represents an event raised when cluster status has been changed.
        /// </summary>
        event ClusterStatusChangedEventHandler ClusterStatusChanged;

        /// <summary>
        /// Represents an event raised when message has been received
        /// by another cluster member using <see cref="EnqueueMessageAsync"/>.
        /// </summary>
        event MessageHandler MessageReceived;

        /// <summary>
        /// Revokes leadership and starts new election process.
        /// </summary>
        /// <returns>The task representing asynchronous result of this operation.</returns>
        Task Resign();

        /// <summary>
        /// Waits until the current node becomes a leader.
        /// </summary>
        /// <param name="timeout"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<bool> WaitForLeadershipAsync(TimeSpan timeout, CancellationToken token);

        /// <summary>
        /// Enqueues replication one-way asynchronous message.
        /// </summary>
        /// <remarks>
        /// The message can placed into queue of the leader node only.
        /// </remarks>
        /// <param name="message">The message to send.</param>
        /// <param name="timeout"></param>
        /// <param name="token"></param>
        /// <returns>The task representing asynchronous execution of the method.</returns>
        /// <exception cref="InvalidOperationException">This node is not the leader.</exception>
        /// <exception cref="ClusterSynchronizationException">The message was not delivered to one or more members.</exception>
        Task EnqueueMessageAsync(IMessage message, TimeSpan timeout, CancellationToken token);
    }
}
