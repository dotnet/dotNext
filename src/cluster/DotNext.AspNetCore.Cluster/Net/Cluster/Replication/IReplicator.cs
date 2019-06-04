using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    using IMessage = Messaging.IMessage;

    /// <summary>
    /// Represents replication handler that can be registered in DI container.
    /// </summary>
    public interface IReplicator
    {
        /// <summary>
        /// Handles transaction log entries from leader node.
        /// </summary>
        /// <param name="leader">The leader node.</param>
        /// <param name="entries">The message representing containing log entries.</param>
        /// <returns>The task representing asynchronous state of this operation.</returns>
        Task AppendEntries(IClusterMember leader, IMessage entries);
    }
}