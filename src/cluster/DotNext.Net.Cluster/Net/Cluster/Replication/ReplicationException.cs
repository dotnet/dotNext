using System;
using System.Runtime.Serialization;

namespace DotNext.Net.Cluster.Replication
{
    /// <summary>
    /// Represents exception caused during replication of the local changes between
    /// cluster leader and other members.
    /// </summary>
    [Serializable]
    public class ReplicationException : ConsensusProtocolException
    {
        internal ReplicationException(IClusterMember member)
            : this(member, ExceptionMessages.ReplicationRejected)
        {

        }

        /// <summary>
        /// Initializes a new replication exception.
        /// </summary>
        /// <param name="member">The member that failed to replicate.</param>
        /// <param name="message">Human-readable description of the issue.</param>
        protected ReplicationException(IClusterMember member, string message) : base(message) => Member = member;

        /// <summary>
        /// Deserialization constructor.
        /// </summary>
        /// <param name="info">The serialized information about object.</param>
        /// <param name="context">The deserialization context.</param>
        protected ReplicationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        /// <summary>
        /// Gets the member that failed to replicate.
        /// </summary>
        public IClusterMember Member { get; }
    }
}
