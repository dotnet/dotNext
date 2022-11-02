using Microsoft.AspNetCore.Connections;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using IO;
using Membership;

internal partial class RaftHttpCluster
{
    private sealed class ReceivedClusterConfiguration : MemoryTransferObject, IClusterConfiguration
    {
        internal ReceivedClusterConfiguration(int length)
            : base(length)
        {
        }

        public long Fingerprint { get; init; }

        long IClusterConfiguration.Length => Content.Length;
    }

    private readonly ClusterMemberAnnouncer<UriEndPoint>? announcer;
    private Task pollingLoopTask = Task.CompletedTask;

    private async Task ConfigurationPollingLoop()
    {
        await foreach (var eventInfo in configurationEvents.Reader.ReadAllAsync(LifecycleToken).ConfigureAwait(false))
        {
            if (eventInfo.IsAdded)
            {
                var member = CreateMember(eventInfo.Id, eventInfo.Address);
                if (await AddMemberAsync(member, LifecycleToken).ConfigureAwait(false))
                    member.IsRemote = eventInfo.Address != localNode;
                else
                    member.Dispose();
            }
            else
            {
                var member = await RemoveMemberAsync(eventInfo.Id, LifecycleToken).ConfigureAwait(false);
                if (member is not null)
                {
                    member.CancelPendingRequests();
                    member.Dispose();
                }
            }
        }
    }

    private async Task<bool> AddMemberAsync(ClusterMemberId id, UriEndPoint address, CancellationToken token)
    {
        using var member = CreateMember(id, address);
        member.IsRemote = EndPointComparer.Equals(localNode, address) is false;
        return await AddMemberAsync(member, warmupRounds, ConfigurationStorage, static m => m.EndPoint, token).ConfigureAwait(false);
    }

    Task<bool> IRaftHttpCluster.AddMemberAsync(ClusterMemberId id, Uri address, CancellationToken token)
        => AddMemberAsync(id, new(address), token);

    Task<bool> IRaftHttpCluster.RemoveMemberAsync(ClusterMemberId id, CancellationToken token)
        => RemoveMemberAsync(id, ConfigurationStorage, token);

    private Task<bool> RemoveMemberAsync(UriEndPoint address, CancellationToken token)
    {
        foreach (var member in Members)
        {
            if (EndPointComparer.Equals(member.EndPoint, address))
            {
                member.CancelPendingRequests();
                return RemoveMemberAsync(member.Id, ConfigurationStorage, token);
            }
        }

        return Task.FromResult<bool>(false);
    }

    Task<bool> IRaftHttpCluster.RemoveMemberAsync(Uri address, CancellationToken token)
        => RemoveMemberAsync(new(address), token);
}