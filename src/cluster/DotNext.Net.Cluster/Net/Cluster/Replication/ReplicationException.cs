using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DotNext.Net.Cluster.Replication
{
    [Serializable]
    public abstract class ReplicationException : ConsensusProtocolException
    {
        protected ReplicationException(string message)
            : base(message)
        {
        }

        protected ReplicationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        /// <summary>
        /// Gets collection of members that are unavailable during synchronization.
        /// </summary>
        public abstract IReadOnlyCollection<IClusterMember> UnresponsiveMembers { get; }
        
        /// <summary>
        /// Obtains an exception occurred during communication with the cluster member.
        /// </summary>
        /// <param name="memberId">ID of the cluster member.</param>
        /// <returns>The exception representing communication failure.</returns>
        public abstract Exception this[in Guid memberId] { get; }
    }
}
