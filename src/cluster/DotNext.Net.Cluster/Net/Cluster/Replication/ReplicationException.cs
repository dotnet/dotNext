using System;
using System.Runtime.Serialization;

namespace DotNext.Net.Cluster.Replication
{
    [Serializable]
    public class ReplicationException : ConsensusProtocolException
    {
        protected ReplicationException(IClusterMember member, string message)
            : base(message)
        {
            Member = member;
        }

        protected ReplicationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public IClusterMember Member { get; }
    }
}
