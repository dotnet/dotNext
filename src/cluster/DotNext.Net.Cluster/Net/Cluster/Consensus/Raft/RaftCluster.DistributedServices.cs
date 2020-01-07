using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Collections.Specialized;
    using DistributedServices;
    using Messaging;

    public partial class RaftCluster<TMember>
    {
        [StructLayout(LayoutKind.Auto)]
        private struct Sponsor : ISponsor<DistributedLock>, IDisposable
        {
            private FixedSizeSet<ClusterMemberId> availableMembers;

            internal Sponsor(IReadOnlyCollection<TMember> members)
            {
                availableMembers = new FixedSizeSet<ClusterMemberId>(members.Count);
                foreach(var member in members)
                    if(member.Status == ClusterMemberStatus.Available)
                        availableMembers.Add(member.Id);
            }

            LeaseState ISponsor<DistributedLock>.UpdateLease(ref DistributedLock obj)
            {
                if(obj.IsExpired)
                    return LeaseState.Expired;
                if(availableMembers.Contains(obj.Owner))
                {
                    obj.Renew();
                    return LeaseState.Prolonged;
                }
                return LeaseState.Active;
            }

            public void Dispose()
            {
                availableMembers.Dispose();
                this = default;
            }
        }

        /// <summary>
        /// Attempts to create distributed lock provider.
        /// </summary>
        /// <param name="cluster"></param>
        /// <param name="localMember">The instance representing the currently executing cluster member.</param>
        /// <typeparam name="TCluster"></typeparam>
        /// <returns></returns>
        [CLSCompliant(false)]
        protected static DistributedLockProvider? TryCreateLockProvider<TCluster>(TCluster cluster, TMember localMember)
            where TCluster : RaftCluster<TMember>, IMessageBus
            => !localMember.IsRemote && cluster.auditTrail is IDistributedLockEngine engine ? new DistributedLockProvider(engine, cluster, localMember.Id) : null;
    }
}