using System;
using System.Collections.Generic;

namespace DotNext.Net.Cluster
{
    public abstract class ClusterSynchronizationException : Exception
    {
        /// <summary>
        /// Gets collection of members that are unavailable during synchronization.
        /// </summary>
        public abstract IReadOnlyCollection<IClusterMember> UnresponsiveMembers { get; }
        
        /// <summary>
        /// Obtains an exception occurred during communication the the cluster member.
        /// </summary>
        /// <param name="memberId">ID of the cluster member.</param>
        /// <returns>The exception representing communication failure.</returns>
        public abstract Exception this[in Guid memberId] { get; }
    }
}
