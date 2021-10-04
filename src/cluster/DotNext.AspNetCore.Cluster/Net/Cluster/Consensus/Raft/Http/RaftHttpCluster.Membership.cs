namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using IO;
using Membership;
using HttpEndPoint = Net.Http.HttpEndPoint;

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

    private readonly ClusterMemberAnnouncer<HttpEndPoint>? announcer;
    private Task pollingLoopTask;

    private async Task ConfigurationPollingLoop()
    {
        await foreach (var eventInfo in ConfigurationStorage.PollChangesAsync(LifecycleToken))
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

    async Task<bool> IRaftHttpCluster.AddMemberAsync(ClusterMemberId id, HttpEndPoint address, CancellationToken token)
    {
        using var member = CreateMember(id, address);
        member.IsRemote = localNode != address;
        return await AddMemberAsync(member, warmupRounds, ConfigurationStorage, static m => m.EndPoint, token).ConfigureAwait(false);
    }

    Task<bool> IRaftHttpCluster.RemoveMemberAsync(ClusterMemberId id, CancellationToken token)
        => RemoveMemberAsync(id, ConfigurationStorage, token);

    Task<bool> IRaftHttpCluster.RemoveMemberAsync(HttpEndPoint address, CancellationToken token)
    {
        foreach (var member in Members)
        {
            if (member.EndPoint == address)
            {
                member.CancelPendingRequests();
                return RemoveMemberAsync(member.Id, ConfigurationStorage, token);
            }
        }

        return Task.FromResult<bool>(false);
    }
}