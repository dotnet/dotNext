namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Replication;

    public class ReplicationRejectedException : ReplicationException
    {
        public ReplicationRejectedException(IRaftClusterMember member, string message)
            : base(member, message)
        {
        }

        public ReplicationRejectedException(IRaftClusterMember member)
            : this(member, ExceptionMessages.ReplicationRejected)
        {
        }
    }
}
