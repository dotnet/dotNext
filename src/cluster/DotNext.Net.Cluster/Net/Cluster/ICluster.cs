using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Represents cluster node in distributed environment.
    /// </summary>
    public interface ICluster
    {
        /// <summary>
        /// Gets collection of members in the cluster node.
        /// </summary>
        IReadOnlyCollection<IClusterMember> Members { get; }

        /// <summary>
        /// Gets the leader node.
        /// </summary>
        IClusterMember Leader { get; }

        /// <summary>
        /// Gets cluster member represented by entire application.
        /// </summary>
        IClusterMember LocalMember { get; }

        /// <summary>
        /// An event raised when leader has been changed.
        /// </summary>
        event ClusterLeaderChangedEventHandler LeaderChanged;

        /// <summary>
        /// An event raised when cluster member becomes available or unavailable.
        /// </summary>
        event ClusterMemberStatusChanged MemberStatusChanged;

        /// <summary>
        /// Represents an event raised when message has been received
        /// by another cluster member using <see cref="EnqueueMessageAsync"/>.
        /// </summary>
        event MessageHandler MessageReceived;

        /// <summary>
        /// Revokes leadership and starts new election process.
        /// </summary>
        void Resign();

        /// <summary>
        /// Enqueues one-way asynchronous message represents data replication.
        /// </summary>
        /// <remarks>
        /// The message can placed into queue of the leader node only.
        /// </remarks>
        /// <param name="message">The message to send.</param>
        /// <param name="timeout"></param>
        /// <param name="token"></param>
        /// <returns>The task representing asynchronous execution of the method.</returns>
        /// <exception cref="InvalidOperationException">The caller node is not the leader.</exception>
        /// <exception cref="ClusterSynchronizationException">The message was not delivered to one or more members.</exception>
        Task EnqueueMessageAsync(IMessage message, TimeSpan timeout, CancellationToken token);
    }
}
