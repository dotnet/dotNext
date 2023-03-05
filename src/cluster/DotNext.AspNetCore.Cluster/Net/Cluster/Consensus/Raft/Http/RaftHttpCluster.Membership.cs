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
            RaftClusterMember? member;
            if (eventInfo.Item2)
            {
                member = CreateMember(eventInfo.Item1);
                if (!await AddMemberAsync(member, LifecycleToken).ConfigureAwait(false))
                    member.Dispose();
            }
            else
            {
                member = await RemoveMemberAsync(ClusterMemberId.FromEndPoint(eventInfo.Item1), LifecycleToken).ConfigureAwait(false);
                if (member is not null)
                {
                    member.CancelPendingRequests();
                    member.Dispose();
                }
            }
        }
    }

    private async Task<bool> AddMemberAsync(UriEndPoint address, CancellationToken token)
    {
        using var member = CreateMember(address);
        return await AddMemberAsync(member, warmupRounds, ConfigurationStorage, GetAddress, token).ConfigureAwait(false);
    }

    private static UriEndPoint GetAddress(RaftClusterMember member) => member.EndPoint;

    Task<bool> IRaftHttpCluster.AddMemberAsync(Uri address, CancellationToken token)
        => AddMemberAsync(new(address), token);

    Task<bool> IRaftHttpCluster.RemoveMemberAsync(Uri address, CancellationToken token)
        => RemoveMemberAsync(ClusterMemberId.FromEndPoint(new UriEndPoint(address)), ConfigurationStorage, GetAddress, token);
}