using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enumerable = System.Linq.Enumerable;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IDistributedApplicationState = IO.Log.IDistributedApplicationState;
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
        /// <value>The collection of exposed distributed services.</value>
        [CLSCompliant(false)]
        protected virtual IEnumerable<DistributedServiceProvider> DistributedServices => Enumerable.Empty<DistributedServiceProvider>();

        /// <summary>
        /// Resolves identifier of the cluster member.
        /// </summary>
        /// <remarks>
        /// If Raft cluster supports distributed services
        /// then this property should be overridden as well as <see cref="DistributedServices"/>.
        /// </remarks>
        /// <param name="member">The member instance.</param>
        /// <returns>The identifier of the member; or <see langword="null"/> if identifier is not known.</returns>
        /// <seealso cref="IO.Log.IDistributedApplicationState.NodeId"/>
        protected virtual Guid? this[TMember member]
        {
            get => !member.IsRemote && auditTrail is IDistributedApplicationState state ? state.NodeId : new Guid?();
        }

        internal async Task ReplicationFinished(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var members = this.members;
        }
    }
}