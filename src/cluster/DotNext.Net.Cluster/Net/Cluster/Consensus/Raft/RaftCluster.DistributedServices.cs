using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

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

        private async Task ProvideSponsorshipAsync(IDistributedObjectManager<DistributedLock> lockManager, CancellationToken token)
        {
            using var sponsor = new Sponsor(members);
            await lockManager.ProvideSponsorshipAsync(sponsor, token).ConfigureAwait(false);
        }

        Task IRaftStateMachine.NotifyBroadcastFinished(CancellationToken token)
            => auditTrail is IDistributedObjectManager<DistributedLock> lockManager ? ProvideSponsorshipAsync(lockManager, token) : Task.CompletedTask;

        /// <summary>
        /// Attempts to create distributed lock provider.
        /// </summary>
        /// <param name="cluster">The implementation of Raft cluster.</param>
        /// <param name="localMember">The instance representing the currently executing cluster member.</param>
        /// <typeparam name="TCluster">The type of Raft implementation.</typeparam>
        /// <returns>The distributed lock provider; or <see langword="null"/> if cluster doesn't support distributed services.</returns>
        [CLSCompliant(false)]
        protected static DistributedLockProvider? TryCreateLockProvider<TCluster>(TCluster cluster, TMember localMember)
            where TCluster : RaftCluster<TMember>, IMessageBus
            => !localMember.IsRemote && cluster.auditTrail is IDistributedLockEngine engine ? new DistributedLockProvider(engine, cluster.LeaderRouter, localMember.Id) : null;
    }
}