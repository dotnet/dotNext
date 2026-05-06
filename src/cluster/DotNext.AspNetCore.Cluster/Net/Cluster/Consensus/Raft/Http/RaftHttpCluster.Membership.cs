using System.Collections.Immutable;
using Microsoft.AspNetCore.Connections;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using Membership;

internal partial class RaftHttpCluster
{
    private readonly ClusterMemberAnnouncer<UriEndPoint>? announcer;
    private Task pollingLoopTask = Task.CompletedTask;

    private async Task ConfigurationPollingLoop()
    {
        await foreach (var configuration in configurationEvents.Reader.ReadAllAsync(LifecycleToken).ConfigureAwait(false))
        {
            await ApplyConfigurationAsync(configuration, LifecycleToken).ConfigureAwait(false);
        }
    }

    private async ValueTask ApplyConfigurationAsync(IClusterConfiguration<UriEndPoint> configuration, CancellationToken token)
    {
        var scope = await ChangeConfigurationAsync(token).ConfigureAwait(false);
        try
        {
            // detect deleted members
            foreach (var member in scope.Members.Values)
            {
                var address = GetAddress(member);
                if (!configuration.Members.Contains(address))
                {
                    scope.MarkAsRemoved(member);
                }
            }
                
            // detect added members
            var addresses = ImmutableHashSet.CreateRange(EndPointComparer, scope.Members.Values.Select(GetAddress));
            foreach (var address in configuration.Members)
            {
                if (!addresses.Contains(address))
                {
                    scope.MarkAsAdded(CreateMember(address));
                }
            }
        }
        finally
        {
            await scope.DisposeAsync().ConfigureAwait(false);
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