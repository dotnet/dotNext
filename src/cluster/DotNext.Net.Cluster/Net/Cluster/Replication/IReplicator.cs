namespace DotNext.Net.Cluster.Replication
{
    using Messaging;

    /// <summary>
    /// Represents cluster with support of data replication.
    /// </summary>
    public interface IReplicationSupport : ICluster
    {
         /// <summary>
        /// Represents an event raised when replication message
        /// is received by cluster nodes from leader node.
        /// </summary>
        event ReplicationEventHandler Replication;

        IAuditTrail AuditTrail { set; }
    }
}