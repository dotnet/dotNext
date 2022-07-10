using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using Membership;
using Messaging;
using Net.Http;
using HttpProtocolVersion = Net.Http.HttpProtocolVersion;
using IClientMetricsCollector = Metrics.IClientMetricsCollector;

[SuppressMessage("Performance", "CA1812", Justification = "This class is instantiated by DI container")]
internal sealed partial class RaftHttpCluster : RaftCluster<RaftClusterMember>, IRaftHttpCluster, IHostedService, IHostingContext
{
    private readonly IClusterMemberLifetime? configurator;
    private readonly IDisposable configurationTracker;
    private readonly IHttpMessageHandlerFactory? httpHandlerFactory;
    private readonly TimeSpan requestTimeout, raftRpcTimeout, connectTimeout;
    private readonly bool openConnectionForEachRequest, coldStart;
    private readonly string clientHandlerName;
    private readonly HttpProtocolVersion protocolVersion;
    private readonly HttpVersionPolicy protocolVersionPolicy;
    private readonly UriEndPoint localNode;
    private readonly int warmupRounds;

    public RaftHttpCluster(
        IOptionsMonitor<HttpClusterMemberConfiguration> config,
        IEnumerable<IInputChannel> messageHandlers,
        ILoggerFactory loggerFactory,
        IClusterMemberLifetime? configurator = null,
        IPersistentState? auditTrail = null,
        IClusterConfigurationStorage<UriEndPoint>? configStorage = null,
        IHttpMessageHandlerFactory? httpHandlerFactory = null,
        MetricsCollector? metrics = null,
        ClusterMemberAnnouncer<UriEndPoint>? announcer = null)
        : this(config, messageHandlers, loggerFactory.CreateLogger<RaftHttpCluster>(), configurator, auditTrail, configStorage, httpHandlerFactory, metrics, announcer)
    {
    }

    internal RaftHttpCluster(
        IOptionsMonitor<HttpClusterMemberConfiguration> config,
        IEnumerable<IInputChannel> messageHandlers,
        ILogger logger,
        IClusterMemberLifetime? configurator = null,
        IPersistentState? auditTrail = null,
        IClusterConfigurationStorage<UriEndPoint>? configStorage = null,
        IHttpMessageHandlerFactory? httpHandlerFactory = null,
        MetricsCollector? metrics = null,
        ClusterMemberAnnouncer<UriEndPoint>? announcer = null)
        : base(config.CurrentValue)
    {
        openConnectionForEachRequest = config.CurrentValue.OpenConnectionForEachRequest;
        metadata = new MemberMetadata(config.CurrentValue.Metadata);
        requestTimeout = config.CurrentValue.RequestTimeout;
        raftRpcTimeout = config.CurrentValue.RpcTimeout;
        connectTimeout = TimeSpan.FromMilliseconds(config.CurrentValue.LowerElectionTimeout);
        duplicationDetector = new DuplicateRequestDetector(config.CurrentValue.RequestJournal);
        clientHandlerName = config.CurrentValue.ClientHandlerName;
        protocolVersion = config.CurrentValue.ProtocolVersion;
        protocolVersionPolicy = config.CurrentValue.ProtocolVersionPolicy;
        localNode = new(config.CurrentValue.PublicEndPoint ?? throw new RaftProtocolException(ExceptionMessages.UnknownLocalNodeAddress));
        ProtocolPath = new(RaftClusterMember.GetProtocolPath(localNode.Uri));
        coldStart = config.CurrentValue.ColdStart;
        warmupRounds = config.CurrentValue.WarmupRounds;

        if (raftRpcTimeout > requestTimeout)
            throw new RaftProtocolException(ExceptionMessages.InvalidRpcTimeout);

        // dependencies
        this.configurator = configurator;
        this.messageHandlers = ImmutableList.CreateRange(messageHandlers);
        AuditTrail = auditTrail ?? new ConsensusOnlyState();
        ConfigurationStorage = configStorage ?? new InMemoryClusterConfigurationStorage();
        this.httpHandlerFactory = httpHandlerFactory;
        Logger = logger;
        Metrics = metrics;
        this.announcer = announcer;

        // track changes in configuration, do not track membership
        configurationTracker = config.OnChange(ConfigurationChanged);
    }

    protected override IClusterConfigurationStorage<UriEndPoint> ConfigurationStorage { get; }

    /// <inheritdoc />
    IReadOnlyCollection<ISubscriber> IMessageBus.Members => Members;

    private RaftClusterMember CreateMember(in ClusterMemberId id, UriEndPoint address)
    {
        var result = new RaftClusterMember(this, address, id)
        {
            Timeout = requestTimeout,
            Metrics = Metrics as IClientMetricsCollector,
        };

        result.DefaultRequestHeaders.ConnectionClose = openConnectionForEachRequest;
        result.DefaultVersionPolicy = protocolVersionPolicy;
        result.IsRemote = !Equals(result.BaseAddress, localNode);
        result.SetProtocolVersion(protocolVersion);
        return result;
    }

    internal PathString ProtocolPath { get; }

    protected sealed override ILogger Logger { get; }

    ILogger IHostingContext.Logger => Logger;

    ISubscriber? IMessageBus.Leader => Leader;

    private void ConfigurationChanged(HttpClusterMemberConfiguration configuration, string name)
    {
        metadata = new MemberMetadata(configuration.Metadata);
    }

    IReadOnlyDictionary<string, string> IHostingContext.Metadata => metadata;

    bool IHostingContext.IsLeader(IRaftClusterMember member) => ReferenceEquals(Leader, member);

    ref readonly ClusterMemberId IHostingContext.LocalMember => ref LocalMemberId;

    HttpMessageHandler IHostingContext.CreateHttpHandler()
        => httpHandlerFactory?.CreateHandler(clientHandlerName) ?? new SocketsHttpHandler { ConnectTimeout = connectTimeout };

    bool IHostingContext.UseEfficientTransferOfLogEntries => AuditTrail.IsLogEntryLengthAlwaysPresented;

    Uri IRaftHttpCluster.LocalMemberAddress => localNode.Uri;

    public override async Task StartAsync(CancellationToken token)
    {
        configurator?.OnStart(this, metadata);

        if (coldStart)
        {
            // in case of cold start, add the local member to the configuration
            var localMember = CreateMember(LocalMemberId, localNode);
            localMember.IsRemote = false;
            await AddMemberAsync(localMember, token).ConfigureAwait(false);
            await ConfigurationStorage.AddMemberAsync(LocalMemberId, localNode, token).ConfigureAwait(false);
            await ConfigurationStorage.ApplyAsync(token).ConfigureAwait(false);
        }
        else
        {
            await ConfigurationStorage.LoadConfigurationAsync(token).ConfigureAwait(false);

            foreach (var (id, address) in ConfigurationStorage.ActiveConfiguration)
            {
                var member = CreateMember(id, address);
                member.IsRemote = EndPointComparer.Equals(localNode, address) is false;
                await AddMemberAsync(member, token).ConfigureAwait(false);
            }
        }

        pollingLoopTask = ConfigurationPollingLoop();
        await base.StartAsync(token).ConfigureAwait(false);

        if (!coldStart && announcer is not null)
            await announcer(LocalMemberId, localNode, token).ConfigureAwait(false);

        StartFollowing();
    }

    public override Task StopAsync(CancellationToken token)
    {
        configurator?.OnStop(this);
        duplicationDetector.Trim(100);
        return base.StopAsync(token);
    }

    /// <inheritdoc />
    ISubscriber? IPeerMesh<ISubscriber>.TryGetPeer(EndPoint peer)
    {
        foreach (var member in Members)
        {
            if (Equals(member.EndPoint, peer))
                return member;
        }

        return null;
    }

    private void Cleanup()
    {
        configurationTracker.Dispose();
        duplicationDetector.Dispose();
        messageHandlers = ImmutableList<IInputChannel>.Empty;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Cleanup();

        base.Dispose(disposing);
    }

    protected override ValueTask DisposeAsyncCore()
    {
        Cleanup();
        return base.DisposeAsyncCore();
    }
}