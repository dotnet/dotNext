using System;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Replication;

    public class MemberUnavailableException : ReplicationException
    {
        private readonly Exception innerException;

        public MemberUnavailableException(IRaftClusterMember member, string message)
            : this(member, message, null)
        {
        }

        public MemberUnavailableException(IRaftClusterMember member, string message, Exception innerException)
            : base(member, message)
            => this.innerException = innerException;

        public override Exception GetBaseException() => innerException ?? base.GetBaseException();
    }
}
