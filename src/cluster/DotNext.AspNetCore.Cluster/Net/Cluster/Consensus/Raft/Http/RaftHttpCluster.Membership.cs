using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Net.Cluster.Consensus.Raft.Membership;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Text;

    internal partial class RaftHttpCluster
    {
        [Obsolete]
        private readonly IMemberDiscoveryService? discoveryService;
        private readonly ClusterMemberAnnouncer<Uri>? announcer;
        private IDisposable? membershipWatch;

        [Obsolete]
        private protected abstract ValueTask<ICollection<EndPoint>> GetHostingAddressesAsync();

        [Obsolete]
        private async ValueTask<Uri?> DetectLocalMemberAsync(CancellationToken token)
        {
            var selector = configurator?.LocalMemberSelector;
            RaftClusterMember? member;

            if (selector is null)
            {
                var addresses = await GetHostingAddressesAsync().ConfigureAwait(false);
                member = FindMember(addresses.Contains);
            }
            else
            {
                member = await FindMemberAsync(selector, token).ConfigureAwait(false);
            }

            if (member is not null)
            {
                member.IsRemote = false;
                return member.BaseAddress;
            }

            return null;
        }

        [Obsolete]
        private void ChangeMembers(in MemberCollectionBuilder builder, ISet<Uri> members)
        {
            var existingMembers = new HashSet<Uri>();

            // remove members
            foreach (var holder in builder)
            {
                Debug.Assert(holder.Member.BaseAddress is not null);
                if (members.Contains(holder.Member.BaseAddress))
                {
                    existingMembers.Add(holder.Member.BaseAddress);
                }
                else if (holder.Member.IsRemote)
                {
                    using var member = holder.Remove();
                    OnMemberRemoved(member);
                    member.CancelPendingRequests();
                }
            }

            // add new members
            foreach (var memberUri in members)
            {
                if (!existingMembers.Contains(memberUri))
                {
                    var member = CreateMember(memberUri, null);
                    builder.Add(member);
                    OnMemberAdded(member);
                }
            }

            // help GC
            existingMembers.Clear();
        }

        [Obsolete]
        private async Task DiscoverMembersAsync(IMemberDiscoveryService discovery, CancellationToken token)
        {
            // cache delegate instance to avoid allocations
            MemberCollectionMutator<ISet<Uri>> mutator = ChangeMembers;

            var members = (await discovery.DiscoverAsync(token).ConfigureAwait(false)).ToImmutableHashSet();
            await ChangeMembersAsync(mutator, members, token).ConfigureAwait(false);

            // start watching (Token should be used here as long-living cancellation token associated with this instance)
            membershipWatch = await discovery.WatchAsync(ApplyChanges, token).ConfigureAwait(false);

            Task ApplyChanges(IReadOnlyCollection<Uri> members, CancellationToken token)
                => ChangeMembersAsync(mutator, members.ToImmutableHashSet(), token);
        }

        protected sealed override async ValueTask AddLocalMemberAsync(Func<AddMemberLogEntry, CancellationToken, ValueTask<long>> appender, CancellationToken token)
        {
            if (localNode is not null)
            {
                using var buffer = Encoding.UTF8.GetBytes(localNode.ToString().AsSpan());
                await appender(new(LocalMemberId, buffer.Memory, Term), token).ConfigureAwait(false);
            }
        }

        // TODO: Remove localNode condition in .NEXT 4
        protected sealed override Task AnnounceAsync(CancellationToken token = default)
            => localNode is null || announcer is null ? Task.CompletedTask : announcer(LocalMemberId, localNode, token);
    }
}