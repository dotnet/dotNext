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

        internal async Task ReplicationFinished(CancellationToken token)
        {
            var members = this.members;
        }
    }
}