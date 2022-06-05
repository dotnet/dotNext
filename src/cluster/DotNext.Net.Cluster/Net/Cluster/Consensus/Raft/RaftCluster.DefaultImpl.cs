using System.Collections.Immutable;
using System.Net;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO;
using IO.Log;
using Membership;
using TransportServices;
using IClientMetricsCollector = Metrics.IClientMetricsCollector;

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

    private readonly ImmutableDictionary<string, string> metadata;
    private readonly Func<ILocalMember, IPEndPoint, ClusterMemberId, IClientMetricsCollector?, RaftClusterMember> clientFactory;
    private readonly Func<ILocalMember, IServer> serverFactory;
    private readonly MemoryAllocator<byte>? allocator;
    private readonly ClusterMemberAnnouncer<IPEndPoint>? announcer;
    private readonly int warmupRounds;
    private readonly bool coldStart;
    private readonly ClusterConfiguration cachedConfig;
    private Task pollingLoopTask;
    private IServer? server;

    /// <summary>
    /// Initializes a new default implementation of Raft-based cluster.
    /// </summary>
    /// <param name="configuration">The configuration of the cluster.</param>
    public RaftCluster(NodeConfiguration configuration)
        : base(configuration)
    {
        Metrics = configuration.Metrics;
        metadata = ImmutableDictionary.CreateRange(StringComparer.Ordinal, configuration.Metadata);
        clientFactory = configuration.CreateMemberClient;
        serverFactory = configuration.CreateServer;
        LocalMemberAddress = configuration.PublicEndPoint;
        allocator = configuration.MemoryAllocator;
        announcer = configuration.Announcer;
        warmupRounds = configuration.WarmupRounds;
        coldStart = configuration.ColdStart;
        ConfigurationStorage = configuration.ConfigurationStorage ?? new InMemoryClusterConfigurationStorage(allocator);
        pollingLoopTask = Task.CompletedTask;
        cachedConfig = new();
    }

    /// <summary>
    /// Gets the address of the local member.
    /// </summary>
    public IPEndPoint LocalMemberAddress { get; }

    /// <inheritdoc />
    protected sealed override IClusterConfigurationStorage<IPEndPoint> ConfigurationStorage { get; }

    /// <summary>
    /// Starts serving local member.
    /// </summary>
    /// <param name="token">The token that can be used to cancel initialization process.</param>
    /// <returns>The task representing asynchronous execution of the method.</returns>
    public override async Task StartAsync(CancellationToken token = default)
    {
        if (coldStart)
        {
            // in case of cold start, add the local member to the configuration
            var localMember = CreateMember(LocalMemberId, LocalMemberAddress);
            localMember.IsRemote = false;
            await AddMemberAsync(localMember, token).ConfigureAwait(false);
            await ConfigurationStorage.AddMemberAsync(LocalMemberId, LocalMemberAddress, token).ConfigureAwait(false);
            await ConfigurationStorage.ApplyAsync(token).ConfigureAwait(false);
        }
        else
        {
            await ConfigurationStorage.LoadConfigurationAsync(token).ConfigureAwait(false);

            foreach (var (id, address) in ConfigurationStorage.ActiveConfiguration)
            {
                var member = CreateMember(id, address);
                member.IsRemote = !Equals(address, LocalMemberAddress);
                await AddMemberAsync(member, token).ConfigureAwait(false);
            }
        }

        pollingLoopTask = ConfigurationPollingLoop();
        await base.StartAsync(token).ConfigureAwait(false);
        server = serverFactory(this);
        server.Start();
        StartFollowing();

        if (!coldStart && announcer is not null)
            await announcer(LocalMemberId, LocalMemberAddress, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops serving local member.
    /// </summary>
    /// <param name="token">The token that can be used to cancel shutdown process.</param>
    /// <returns>The task representing asynchronous execution of the method.</returns>
    public override Task StopAsync(CancellationToken token = default)
    {
        server?.Dispose();
        server = null;
        return base.StopAsync(token);
    }

    private RaftClusterMember CreateMember(ClusterMemberId id, IPEndPoint address)
        => clientFactory.Invoke(this, address, id, Metrics as IClientMetricsCollector);

    /// <summary>
    /// Announces a new member in the cluster.
    /// </summary>
    /// <param name="id">The identifier of the cluster member.</param>
    /// <param name="address">The addres of the cluster member.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the node has been added to the cluster successfully;
    /// <see langword="false"/> if the node rejects the replication or the address of the node cannot be committed.
    /// </returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled or the cluster elects a new leader.</exception>
    public async Task<bool> AddMemberAsync(ClusterMemberId id, IPEndPoint address, CancellationToken token = default)
    {
        using var member = CreateMember(id, address);
        member.IsRemote = !Equals(LocalMemberAddress, address);
        return await AddMemberAsync(member, warmupRounds, ConfigurationStorage, static m => m.EndPoint, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the member from the cluster.
    /// </summary>
    /// <param name="address">The cluster member to remove.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the node has been removed from the cluster successfully;
    /// <see langword="false"/> if the node rejects the replication or the address of the node cannot be committed.
    /// </returns>
    public Task<bool> RemoveMemberAsync(IPEndPoint address, CancellationToken token = default)
    {
        foreach (var member in Members)
        {
            if (Equals(member.EndPoint, address))
                return RemoveMemberAsync(member.Id, ConfigurationStorage, token);
        }

        return Task.FromResult<bool>(false);
    }

    private async Task ConfigurationPollingLoop()
    {
        await foreach (var eventInfo in ConfigurationStorage.PollChangesAsync(LifecycleToken).ConfigureAwait(false))
        {
            if (eventInfo.IsAdded)
            {
                var member = CreateMember(eventInfo.Id, eventInfo.Address);
                if (await AddMemberAsync(member, LifecycleToken).ConfigureAwait(false))
                    member.IsRemote = !Equals(eventInfo.Address, LocalMemberAddress);
                else
                    member.Dispose();
            }
            else
            {
                var member = await RemoveMemberAsync(eventInfo.Id, LifecycleToken).ConfigureAwait(false);
                if (member is not null)
                {
                    await member.CancelPendingRequestsAsync().ConfigureAwait(false);
                    member.Dispose();
                }
            }
        }
    }

    /// <inheritdoc />
    bool ILocalMember.IsLeader(IRaftClusterMember member) => ReferenceEquals(Leader, member);

    /// <inheritdoc />
    ref readonly ClusterMemberId ILocalMember.Id => ref LocalMemberId;

    /// <inheritdoc />
    IReadOnlyDictionary<string, string> ILocalMember.Metadata => metadata;

    /// <inheritdoc />
    Task<bool> ILocalMember.ResignAsync(CancellationToken token)
        => ResignAsync(token);

    /// <inheritdoc />
    async Task ILocalMember.ProposeConfigurationAsync(Func<Memory<byte>, CancellationToken, ValueTask> configurationReader, long configurationLength, long fingerprint, CancellationToken token)
    {
        var buffer = allocator.Invoke(configurationLength.Truncate(), true);
        await configurationReader(buffer.Memory, token).ConfigureAwait(false);
        cachedConfig.Clear();
        cachedConfig.Update(buffer, fingerprint);
    }

    /// <inheritdoc />
    async Task<Result<bool>> ILocalMember.AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, long? fingerprint, bool applyConfig, CancellationToken token)
    {
        TryGetMember(sender)?.Touch();
        Result<bool> result;

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
    Task<Result<bool>> ILocalMember.AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
        => AppendEntriesAsync(sender, senderTerm, entries, prevLogIndex, prevLogTerm, commitIndex, config, applyConfig, token);

    /// <inheritdoc />
    Task<Result<bool>> ILocalMember.VoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        var member = TryGetMember(sender);
        if (member is null)
            return Task.FromResult(new Result<bool>(Term, false));

        member.Touch();
        return VoteAsync(sender, term, lastLogIndex, lastLogTerm, token);
    }

    /// <inheritdoc />
    Task<Result<PreVoteResult>> ILocalMember.PreVoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        TryGetMember(sender)?.Touch();
        return PreVoteAsync(sender, term + 1L, lastLogIndex, lastLogTerm, token);
    }

    /// <inheritdoc />
    Task<Result<bool>> ILocalMember.InstallSnapshotAsync<TSnapshot>(ClusterMemberId sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
    {
        TryGetMember(sender)?.Touch();

        return InstallSnapshotAsync(sender, senderTerm, snapshot, snapshotIndex, token);
    }

    /// <inheritdoc />
    Task<long?> ILocalMember.SynchronizeAsync(long commitIndex, CancellationToken token)
        => SynchronizeAsync(commitIndex, token);

    private void Cleanup()
    {
        server?.Dispose();
        server = null;

        cachedConfig.Dispose();
    }

    /// <summary>
    /// Releases managed and unmanaged resources associated with this object.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> if called from <see cref="Disposable.Dispose()"/>; <see langword="false"/> if called from finalizer <see cref="Disposable.Finalize()"/>.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Cleanup();

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore().ConfigureAwait(false);
        Cleanup();
    }
}