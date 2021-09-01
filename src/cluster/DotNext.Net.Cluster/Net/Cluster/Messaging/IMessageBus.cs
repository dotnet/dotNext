using System.Collections.Generic;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Specifies a cloud of nodes that can communicate with each other through the network.
    /// </summary>
    public interface IMessageBus : ICluster, IPeerMesh<ISubscriber>
    {
        /// <summary>
        /// Gets the leader node.
        /// </summary>
        new ISubscriber? Leader { get; }

        /// <summary>
        /// Gets a set of visible cluster members.
        /// </summary>
        IReadOnlyCollection<ISubscriber> Members { get; }

        /// <inheritdoc/>
        IClusterMember? ICluster.Leader => Leader;

        /// <summary>
        /// Allows to route messages to the leader
        /// even if it is changed during transmission.
        /// </summary>
        IOutputChannel LeaderRouter { get; }

        /// <summary>
        /// Adds message handler.
        /// </summary>
        /// <param name="handler">The message handler.</param>
        void AddListener(IInputChannel handler);

        /// <summary>
        /// Removes message handler.
        /// </summary>
        /// <param name="handler">The message handler.</param>
        void RemoveListener(IInputChannel handler);
    }
}