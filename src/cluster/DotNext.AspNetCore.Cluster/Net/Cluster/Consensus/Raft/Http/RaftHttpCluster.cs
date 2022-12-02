using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading.Channels;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using Membership;
using Messaging;
using Net.Http;
using IFailureDetector = Diagnostics.IFailureDetector;
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
    private readonly Channel<ClusterConfigurationEvent<UriEndPoint>> configurationEvents;

    public RaftHttpCluster(
        IOptionsMonitor<HttpClusterMemberConfiguration> config,
        IEnumerable<IInputChannel> messageHandlers,
        ILoggerFactory loggerFactory,
        IClusterMemberLifetime? configurator = null,
        IPersistentState? auditTrail = null,
        IClusterConfigurationStorage<UriEndPoint>? configStorage = null,
        IHttpMessageHandlerFactory? httpHandlerFactory = null,
        MetricsCollector? metrics = null,
        ClusterMemberAnnouncer<UriEndPoint>? announcer = null,
        Func<IRaftClusterMember, IFailureDetector>? failureDetectorFactory = null)
        : this(config, messageHandlers, loggerFactory.CreateLogger<RaftHttpCluster>(), configurator, auditTrail, configStorage, httpHandlerFactory, metrics, announcer)
    {
        FailureDetectorFactory = failureDetectorFactory;
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
        ProtocolPath = new(localNode.Uri.GetComponents(UriComponents.Path, UriFormat.Unescaped) is { Length: > 0 } protocolPath
                ? string.Concat("/", protocolPath)
                : RaftClusterMember.DefaultProtocolPath);
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
        configurationEvents = Channel.CreateUnbounded<ClusterConfigurationEvent<UriEndPoint>>(new() { SingleWriter = true, SingleReader = true });
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

    public async Task StartAsync(CancellationToken token)
    {
        configurator?.OnStart(this, metadata);
        ConfigurationStorage.ActiveConfigurationChanged += configurationEvents.Writer.WriteAsync;

        if (coldStart)
        {
            // in case of cold start, add the local member to the configuration
            var localMember = CreateMember(LocalMemberId, localNode);
            Debug.Assert(localMember.IsRemote is false);
            await AddMemberAsync(localMember, token).ConfigureAwait(false);
            await ConfigurationStorage.AddMemberAsync(LocalMemberId, localNode, token).ConfigureAwait(false);
            await ConfigurationStorage.ApplyAsync(token).ConfigureAwait(false);
        }
        else
        {
            await ConfigurationStorage.LoadConfigurationAsync(token).ConfigureAwait(false);

            foreach (var (id, address) in ConfigurationStorage.ActiveConfiguration)
            {
                await AddMemberAsync(CreateMember(id, address), token).ConfigureAwait(false);
            }
        }

        pollingLoopTask = ConfigurationPollingLoop();
        await StartAsync(IsLocalMember, token).ConfigureAwait(false);

        if (!coldStart && announcer is not null)
            await announcer(LocalMemberId, localNode, token).ConfigureAwait(false);

        StartFollowing();
    }

    private ValueTask<bool> IsLocalMember(RaftClusterMember member, CancellationToken token)
        => new(EndPointComparer.Equals(localNode, member.EndPoint));

    public override Task StopAsync(CancellationToken token)
    {
        return LifecycleToken.IsCancellationRequested ? Task.CompletedTask : StopAsync();

        async Task StopAsync()
        {
            try
            {
                configurator?.OnStop(this);
                duplicationDetector.Trim(100);
                ConfigurationStorage.ActiveConfigurationChanged -= configurationEvents.Writer.WriteAsync;
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

    /// <inheritdoc />
    ISubscriber? IPeerMesh<ISubscriber>.TryGetPeer(EndPoint peer)
    {
        foreach (var member in Members)
        {
            if (EndPointComparer.Equals(member.EndPoint, peer))
                return member;
        }

        return null;
    }

    protected override async ValueTask UnavailableMemberDetected(RaftClusterMember member, CancellationToken token)
        => await ConfigurationStorage.RemoveMemberAsync(member.Id, token).ConfigureAwait(false);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            configurationTracker.Dispose();
            duplicationDetector.Dispose();
            messageHandlers = ImmutableList<IInputChannel>.Empty;
            configurationEvents.Writer.TryComplete(new ObjectDisposedException(GetType().Name));
        }

        base.Dispose(disposing);
    }
}