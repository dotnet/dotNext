using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal partial class RaftHttpCluster
    {
        private readonly IMemberDiscoveryService? discoveryService;
        private IDisposable? membershipWatch;

        private protected abstract Task<ICollection<EndPoint>> GetHostingAddressesAsync();

        private async Task<ClusterMemberId> DetectLocalMemberAsync(CancellationToken token)
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

            return member?.Id ?? throw new RaftProtocolException(ExceptionMessages.UnresolvedLocalMember);
        }

        // TODO: ISet<Uri> should be replaced with IReadOnlySet<Uri> in .NET 6
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
                else
                {
                    using var member = holder.Remove();
                    MemberRemoved?.Invoke(this, member);
                    member.CancelPendingRequests();
                }
            }

            // add new members
            foreach (var memberUri in members)
            {
                if (!existingMembers.Contains(memberUri))
                {
                    var member = CreateMember(memberUri);
                    builder.Add(member);
                    MemberAdded?.Invoke(this, member);
                }
            }

            // help GC
            existingMembers.Clear();
        }

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
    }
}