using System.Collections.Frozen;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO;
using IO.Log;
using Membership;
using TransportServices;

/// <summary>
/// Represents default implementation of Raft-based cluster.
/// </summary>
public partial class RaftCluster : RaftCluster<RaftClusterMember>, ILocalMember
{
    [StructLayout(LayoutKind.Auto)]
    private sealed class ClusterConfiguration : Disposable, IClusterConfiguration
    {
        private MemoryOwner<byte> configuration;

        public long Fingerprint { get; private set; }

        internal void Update(MemoryOwner<byte> config, long fingerprint)
        {
            configuration = config;
            Fingerprint = fingerprint;
        }

        internal void Clear()
        {
            configuration.Dispose();
            Fingerprint = 0L;
        }

        long IClusterConfiguration.Length => configuration.Length;

        bool IDataTransferObject.IsReusable => false;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => writer.WriteAsync(configuration.Memory, null, token);

        ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
            => transformation.TransformAsync<SequenceReader>(IAsyncBinaryReader.Create(configuration.Memory), token);

        bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
        {
            memory = configuration.Memory;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Clear();

            base.Dispose(disposing);
        }
    }

    private readonly FrozenDictionary<string, string> metadata;
    private readonly Func<ILocalMember, EndPoint, RaftClusterMember> clientFactory;
    private readonly Func<ILocalMember, IServer> serverFactory;
    private readonly MemoryAllocator<byte>? allocator;
    private readonly ClusterMemberAnnouncer<EndPoint>? announcer;
    private readonly int warmupRounds;
    private readonly bool coldStart;
    private readonly ClusterConfiguration cachedConfig;
    private readonly Channel<(EndPoint, bool)> configurationEvents;
    private readonly ClusterMemberId localMemberId;
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
        allocator = configuration.MemoryAllocator;
        announcer = configuration.Announcer;
        warmupRounds = configuration.WarmupRounds;
        coldStart = configuration.ColdStart;
        ConfigurationStorage = configuration.ConfigurationStorage ?? new InMemoryClusterConfigurationStorage(EndPointComparer, allocator);
        pollingLoopTask = Task.CompletedTask;
        cachedConfig = new();
        Logger = configuration.LoggerFactory.CreateLogger<RaftCluster>();
        configurationEvents = Channel.CreateUnbounded<(EndPoint, bool)>(new() { SingleWriter = true, SingleReader = true });
    }

    private static TagList GetMeasurementTags(NodeConfiguration config) => new()
    {
        { IRaftCluster.LocalAddressMeterAttributeName, config.PublicEndPoint.ToString() },
    };

    /// <inheritdoc />
    protected override ILogger Logger { get; }

    /// <summary>
    /// Gets the address of the local member.
    /// </summary>
    public EndPoint LocalMemberAddress { get; }

    /// <inheritdoc />
    protected sealed override IClusterConfigurationStorage<EndPoint> ConfigurationStorage { get; }

    /// <summary>
    /// Starts serving local member.
    /// </summary>
    /// <param name="token">The token that can be used to cancel initialization process.</param>
    /// <returns>The task representing asynchronous execution of the method.</returns>
    public override async Task StartAsync(CancellationToken token = default)
    {
        ConfigurationStorage.ActiveConfigurationChanged += configurationEvents.Writer.WriteConfigurationEvent;

        if (coldStart)
        {
            // in case of cold start, add the local member to the configuration
            var localMember = CreateMember(LocalMemberAddress);
            await AddMemberAsync(localMember, token).ConfigureAwait(false);
            await ConfigurationStorage.AddMemberAsync(LocalMemberAddress, token).ConfigureAwait(false);
            await ConfigurationStorage.ApplyAsync(token).ConfigureAwait(false);
        }
        else
        {
            await ConfigurationStorage.LoadConfigurationAsync(token).ConfigureAwait(false);

            foreach (var address in ConfigurationStorage.ActiveConfiguration)
            {
                await AddMemberAsync(CreateMember(address), token).ConfigureAwait(false);
            }
        }

        pollingLoopTask = ConfigurationPollingLoop();
        await base.StartAsync(token).ConfigureAwait(false);
        server = serverFactory(this);
        await server.StartAsync(token).ConfigureAwait(false);
        StartFollowing();

        if (!coldStart && announcer is not null)
            await announcer(LocalMemberAddress, metadata, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override ValueTask<bool> DetectLocalMemberAsync(RaftClusterMember candidate, CancellationToken token)
        => new(EndPointComparer.Equals(LocalMemberAddress, candidate.EndPoint));

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
                ConfigurationStorage.ActiveConfigurationChanged -= configurationEvents.Writer.WriteConfigurationEvent;
                configurationEvents.Writer.TryComplete();
                await pollingLoopTask.ConfigureAwait(false);
                pollingLoopTask = Task.CompletedTask;
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
        return await AddMemberAsync(member, warmupRounds, ConfigurationStorage, GetAddress, token).ConfigureAwait(false);
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
        => RemoveMemberAsync(ClusterMemberId.FromEndPoint(address), ConfigurationStorage, GetAddress, token);

    private async Task ConfigurationPollingLoop()
    {
        await foreach (var eventInfo in configurationEvents.Reader.ReadAllAsync(LifecycleToken).ConfigureAwait(false))
        {
            RaftClusterMember? member;
            if (eventInfo.Item2)
            {
                member = CreateMember(eventInfo.Item1);
                if (!await AddMemberAsync(member, CancellationToken.None).ConfigureAwait(false))
                    member.Dispose();
            }
            else
            {
                member = await RemoveMemberAsync(ClusterMemberId.FromEndPoint(eventInfo.Item1), CancellationToken.None).ConfigureAwait(false);
                if (member is not null)
                {
                    await member.CancelPendingRequestsAsync().ConfigureAwait(false);
                    member.Dispose();
                }
            }
        }
    }

    /// <inheritdoc />
    protected sealed override async ValueTask UnavailableMemberDetected(RaftClusterMember member, CancellationToken token)
        => await ConfigurationStorage.RemoveMemberAsync(GetAddress(member), token).ConfigureAwait(false);

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
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))] // hot path, avoid allocations
    async ValueTask ILocalMember.ProposeConfigurationAsync(Func<Memory<byte>, CancellationToken, ValueTask> configurationReader, long configurationLength, long fingerprint, CancellationToken token)
    {
        var buffer = allocator.AllocateExactly(int.CreateSaturating(configurationLength));
        await configurationReader(buffer.Memory, token).ConfigureAwait(false);
        cachedConfig.Clear();
        cachedConfig.Update(buffer, fingerprint);
    }

    /// <inheritdoc />
    async ValueTask<Result<HeartbeatResult>> ILocalMember.AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, long? fingerprint, bool applyConfig, CancellationToken token)
    {
        TryGetMember(sender)?.Touch();
        Result<HeartbeatResult> result;

        try
        {
            IClusterConfiguration configuration;

            if (fingerprint.HasValue)
            {
                configuration = IClusterConfiguration.CreateEmpty(fingerprint.GetValueOrDefault());
            }
            else
            {
                configuration = cachedConfig;
                applyConfig = false;
            }

            result = await AppendEntriesAsync(sender, senderTerm, entries, prevLogIndex, prevLogTerm, commitIndex, configuration, applyConfig, token).ConfigureAwait(false);
        }
        finally
        {
            cachedConfig.Clear();
        }

        return result;
    }

    /// <inheritdoc />
    ValueTask<Result<HeartbeatResult>> ILocalMember.AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
        => AppendEntriesAsync(sender, senderTerm, entries, prevLogIndex, prevLogTerm, commitIndex, config, applyConfig, token);

    /// <inheritdoc />
    ValueTask<Result<bool>> ILocalMember.VoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        if (TryGetMember(sender) is not { } member)
            return ValueTask.FromResult<Result<bool>>(new() { Term = Term });

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
    ValueTask<Result<HeartbeatResult>> ILocalMember.InstallSnapshotAsync<TSnapshot>(ClusterMemberId sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
    {
        TryGetMember(sender)?.Touch();

        return InstallSnapshotAsync(sender, senderTerm, snapshot, snapshotIndex, token);
    }

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
            cachedConfig.Dispose();
            configurationEvents.Writer.TryComplete(CreateException());
        }

        base.Dispose(disposing);
    }
}

file static class RaftClusterHelpers
{
    internal static ValueTask WriteConfigurationEvent(this ChannelWriter<(EndPoint, bool)> writer, EndPoint address, bool isAdded, CancellationToken token)
        => writer.WriteAsync(new(address, isAdded), token);
}