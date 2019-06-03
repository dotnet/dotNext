using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    /// <summary>
    /// Represents audit trail that can be used to detect changes in data source represented by cluster member.
    /// </summary>
    public interface IAuditTrail
    {
        /// <summary>
        /// Obtains checkpoint represents transaction log at the current point in time.
        /// </summary>
        ICheckpoint Checkpoint { get; }
    }
}