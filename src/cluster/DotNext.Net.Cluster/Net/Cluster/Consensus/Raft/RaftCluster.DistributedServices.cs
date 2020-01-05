using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enumerable = System.Linq.Enumerable;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;
    using DistributedServices;

    public partial class RaftCluster<TMember>
    {
        //contains set of available cluster members
        private sealed class Sponsor : ISponsor
        {
            private readonly ReadOnlyMemory<Guid> members;

            public Sponsor(Memory<Guid> members)
                => this.members = members;

            bool ISponsor.IsAvailable(Guid owner)
            {
                return true;
            }
        }

        /// <summary>
        /// Gets all exposed distributed services.
        /// </summary>
        /// <returns>The collection of exposed distributed services.</returns>
        [CLSCompliant(false)]
        protected virtual IEnumerable<DistributedServiceProvider> DistributedServices => Enumerable.Empty<DistributedServiceProvider>();

        /// <summary>
        /// Resolves identifier of 
        /// </summary>
        /// <remarks>
        /// If Raft cluster supports distributed services
        /// then this method should be overridden as well as <see cref="DistributedServices"/>.
        /// </remarks>
        /// <param name="member">The member instance.</param>
        /// <returns>The identifier of the member.</returns>
        /// <seealso cref="PersistentState.NodeId"/>
        protected virtual Guid? GetMemberId(TMember member) => null;

        internal async Task ReplicationFinished(CancellationToken token)
        {
            var members = this.members;
        }
    }
}