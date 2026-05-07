using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;
using Membership;
using NetworkTransport;

/// <summary>
/// Represents default implementation of Raft-based cluster.
/// </summary>
public partial class RaftCluster : RaftCluster<RaftClusterMember>, ILocalMember
{
    private readonly FrozenDictionary<string, string> metadata;
    private readonly Func<ILocalMember, EndPoint, RaftClusterMember> clientFactory;
    private readonly Func<ILocalMember, IServer> serverFactory;
    private readonly ClusterMemberAnnouncer<EndPoint>? announcer;
    private readonly int warmupRounds;
    private readonly bool coldStart;
    private readonly ClusterMemberId localMemberId;
    private readonly IClusterConfigurationStorage<EndPoint> configurationStorage;
    private readonly Channel<IClusterConfiguration<EndPoint>> configurationEvents;
    private Task pollingLoopTask;
    private IServer? server;

    /// <summary>
    /// Initializes a new default implementation of Raft-based cluster.
    /// </summary>
    /// <param name="configuration">The configuration of the cluster.</param>
    public RaftCluster(NodeConfiguration configuration)
        : base(configuration, GetMeasurementTags(configuration))
    {
        metadata = configuration.Metadata.ToFrozenDictionary(StringComparer.Ordinal);
        clientFactory = configuration.CreateClient;
        serverFactory = configuration.CreateServer;
        localMemberId = ClusterMemberId.FromEndPoint(LocalMemberAddress = configuration.PublicEndPoint);
        announcer = configuration.Announcer;
        warmupRounds = configuration.WarmupRounds;
        coldStart = configuration.ColdStart;
        configurationStorage = configuration.ConfigurationStorage;
        configurationEvents = Channel.CreateUnbounded<IClusterConfiguration<EndPoint>>(new()
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = true,
        });
        pollingLoopTask = Task.CompletedTask;
        Logger = configuration.LoggerFactory.CreateLogger<RaftCluster>();
    }

    private static TagList GetMeasurementTags(NodeConfiguration config) => new()
    {
        { IRaftCluster.LocalAddressMeterAttributeName, config.PublicEndPoint.ToString() },
    };

    /// <inheritdoc />
    protected override ILogger Logger { get; }

    /// <inheritdoc />
    ILogger ILocalMember.Logger => Logger;

    /// <summary>
    /// Gets the address of the local member.
    /// </summary>
    public EndPoint LocalMemberAddress { get; }

    /// <summary>
    /// Starts serving local member.
    /// </summary>
    /// <param name="token">The token that can be used to cancel initialization process.</param>
    /// <returns>The task representing asynchronous execution of the method.</returns>
    public override async Task StartAsync(CancellationToken token = default)
    {
        IRaftCluster.SetConfigurationStorage(AuditTrail, configurationStorage);
        var config = await configurationStorage.LoadConfigurationAsync(token).ConfigureAwait(false);
        var announcementNeeded = true;
        if (coldStart && config.Members.Count is 0)
        {
            // in case of cold start, add the local member to the configuration
            config = config.Add(LocalMemberAddress);
            await configurationStorage.SaveConfigurationAsync(config, configurationVersion: 0L, token).ConfigureAwait(false);
            announcementNeeded = false;
        }

        await ApplyConfigurationAsync(config, token).ConfigureAwait(false);
        configurationStorage.ConfigurationChanged += configurationEvents.Writer.WriteAsync;
        pollingLoopTask = ConfigurationPollingLoop();
        await base.StartAsync(token).ConfigureAwait(false);
        server = serverFactory(this);
        await server.StartAsync(token).ConfigureAwait(false);
        StartFollowing();

        if (announcementNeeded && announcer is not null)
            await announcer(LocalMemberAddress, metadata, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops serving local member.
    /// </summary>
    /// <param name="token">The token that can be used to cancel shutdown process.</param>
    /// <returns>The task representing asynchronous execution of the method.</returns>
    public override Task StopAsync(CancellationToken token = default)
    {
        return LifecycleToken.IsCancellationRequested ? Task.CompletedTask : StopAsync();

        async Task StopAsync()
        {
            try
            {
                await (server?.DisposeAsync() ?? ValueTask.CompletedTask).ConfigureAwait(false);
                server = null;
                configurationStorage.ConfigurationChanged -= configurationEvents.Writer.WriteAsync;
                configurationEvents.Writer.TryComplete();
                await pollingLoopTask.ConfigureAwait(false);
                pollingLoopTask = Task.CompletedTask;
            }
            catch (Exception e)
            {
                configurationEvents.Writer.TryComplete(e);
            }
            finally
            {
                await base.StopAsync(token).ConfigureAwait(false);
            }
        }
    }

    private RaftClusterMember CreateMember(EndPoint address)
        => clientFactory.Invoke(this, address);

    /// <summary>
    /// Announces a new member in the cluster.
    /// </summary>
    /// <param name="address">The address of the cluster member.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the node has been added to the cluster successfully;
    /// <see langword="false"/> if the node rejects the replication or the address of the node cannot be committed.
    /// </returns>
    /// <exception cref="NotLeaderException">The current node is not a leader.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled or the cluster elects a new leader.</exception>
    public async Task<bool> AddMemberAsync(EndPoint address, CancellationToken token = default)
    {
        using var member = CreateMember(address);
        return await AddMemberAsync(member, warmupRounds, configurationStorage, GetAddress, token).ConfigureAwait(false);
    }

    private static EndPoint GetAddress(RaftClusterMember member) => member.EndPoint;

    /// <summary>
    /// Removes the member from the cluster.
    /// </summary>
    /// <param name="address">The cluster member to remove.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the node has been removed from the cluster successfully;
    /// <see langword="false"/> if the node rejects the replication or the address of the node cannot be committed.
    /// </returns>
    /// <exception cref="NotLeaderException">The current node is not a leader.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled or the cluster elects a new leader.</exception>
    public Task<bool> RemoveMemberAsync(EndPoint address, CancellationToken token = default)
        => RemoveMemberAsync(ClusterMemberId.FromEndPoint(address), configurationStorage, GetAddress, token);

    private async Task ConfigurationPollingLoop()
    {
        await foreach (var configuration in configurationEvents.Reader.ReadAllAsync(LifecycleToken).ConfigureAwait(false))
        {
            await ApplyConfigurationAsync(configuration, LifecycleToken).ConfigureAwait(false);
        }
    }

    private async ValueTask ApplyConfigurationAsync(IClusterConfiguration<EndPoint> configuration, CancellationToken token)
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

    /// <inheritdoc />
    protected sealed override ValueTask UnavailableMemberDetected(RaftClusterMember member, long term, CancellationToken token)
        => UnavailableMemberDetected(configurationStorage, GetAddress(member), term, token);

    /// <inheritdoc />
    bool ILocalMember.IsLeader(IRaftClusterMember member) => ReferenceEquals(Leader, member);

    /// <inheritdoc />
    ref readonly ClusterMemberId ILocalMember.Id => ref localMemberId;

    /// <inheritdoc />
    IReadOnlyDictionary<string, string> ILocalMember.Metadata => metadata;

    /// <inheritdoc />
    ValueTask<bool> ILocalMember.ResignAsync(CancellationToken token)
        => ResignAsync(token);

    /// <inheritdoc />
    ValueTask<Result<HeartbeatResult>> ILocalMember.AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
        => AppendEntriesAsync(sender, senderTerm, entries, prevLogIndex, prevLogTerm, commitIndex, token);

    /// <inheritdoc />
    ValueTask<Result<bool>> ILocalMember.VoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        if (TryGetMember(sender) is not { } member)
            return ValueTask.FromResult<Result<bool>>(new() { Term = AuditTrail.Term });

        member.Touch();
        return VoteAsync(sender, term, lastLogIndex, lastLogTerm, token);
    }

    /// <inheritdoc />
    ValueTask<Result<PreVoteResult>> ILocalMember.PreVoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        TryGetMember(sender)?.Touch();
        return PreVoteAsync(sender, term + 1L, lastLogIndex, lastLogTerm, token);
    }

    /// <inheritdoc />
    ValueTask<Result<HeartbeatResult>> ILocalMember.InstallSnapshotAsync<TSnapshot>(ClusterMemberId sender, long senderTerm, TSnapshot snapshot, 
        long snapshotIndex, CancellationToken token)
    {
        TryGetMember(sender)?.Touch();

        return InstallSnapshotAsync(sender, senderTerm, snapshot, snapshotIndex, token);
    }

    /// <inheritdoc />
    ValueTask<bool> ILocalMember.InstallConfigurationAsync<TConfiguration>(long senderTerm, TConfiguration configuration,
        long configurationVersion, CancellationToken token)
        => InstallConfigurationAsync(senderTerm, configuration, configurationVersion, token);

    /// <inheritdoc />
    ValueTask<long?> ILocalMember.SynchronizeAsync(long commitIndex, CancellationToken token)
        => SynchronizeAsync(commitIndex, token);

    /// <summary>
    /// Releases managed and unmanaged resources associated with this object.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> if called from <see cref="Disposable.Dispose()"/>; <see langword="false"/> if called from finalizer <see cref="Disposable.Finalize()"/>.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            server?.Dispose();
            server = null;
        }

        base.Dispose(disposing);
    }
}