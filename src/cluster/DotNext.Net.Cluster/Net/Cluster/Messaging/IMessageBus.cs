using System.Collections.Generic;
using System.Net;

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

        /// <inheritdoc/>
        IClusterMember? ICluster.Leader => Leader;

        /// <summary>
        /// Represents a collection of nodes in the network.
        /// </summary>
        new IReadOnlyCollection<ISubscriber> Members { get; }

        /// <inheritdoc/>
        IReadOnlyCollection<IClusterMember> ICluster.Members => Members;

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

        /// <inheritdoc/>
        ISubscriber? IPeerMesh<ISubscriber>.TryGetPeer(EndPoint peer)
        {
            foreach (var member in Members)
            {
                if (Equals(member.EndPoint, peer))
                    return member;
            }

            return null;
        }
    }
}