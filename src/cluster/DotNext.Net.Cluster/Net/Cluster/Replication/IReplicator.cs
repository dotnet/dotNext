namespace DotNext.Net.Cluster.Replication
{
    /// <summary>
    /// Represents cluster with support of data replication.
    /// </summary>
    public interface IReplicator : ICluster
    {
         /// <summary>
        /// Represents an event raised when replication message
        /// is received by cluster nodes from leader node.
        /// </summary>
        event ReplicationEventHandler Replication;

        /// <summary>
        /// Specifies transaction log to be tracked by this replicator.
        /// </summary>
        IAuditTrail AuditTrail { set; }
    }
}