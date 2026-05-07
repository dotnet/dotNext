using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading.Channels;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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

[SuppressMessage("Performance", "CA1812", Justification = "This class is instantiated by DI container")]
internal sealed partial class RaftHttpCluster : RaftCluster<RaftClusterMember>, IRaftHttpCluster, IHostedService, IHostingContext
{
    private readonly IClusterMemberLifetime? configurator;
    private readonly IDisposable? configurationTracker;
    private readonly IHttpMessageHandlerFactory? httpHandlerFactory;
    private readonly TimeSpan requestTimeout, raftRpcTimeout, connectTimeout;
    private readonly bool openConnectionForEachRequest, coldStart;
    private readonly string clientHandlerName;
    private readonly HttpProtocolVersion protocolVersion;
    private readonly HttpVersionPolicy protocolVersionPolicy;
    private readonly UriEndPoint localNode;
    private readonly ClusterMemberId localNodeId;
    private readonly int warmupRounds;
    private readonly Channel<IClusterConfiguration<UriEndPoint>> configurationEvents;

    private RaftHttpCluster(
        IOptionsMonitor<HttpClusterMemberConfiguration> config,
        ILogger logger)
        : base(config.CurrentValue, GetMeasurementTags(config.CurrentValue, out var localNode))
    {
        syncRoot = new();
        openConnectionForEachRequest = config.CurrentValue.OpenConnectionForEachRequest;
        metadata = new(config.CurrentValue.Metadata);
        requestTimeout = config.CurrentValue.RequestTimeout;
        raftRpcTimeout = config.CurrentValue.RpcTimeout;
        connectTimeout = TimeSpan.FromMilliseconds(config.CurrentValue.LowerElectionTimeout);
        duplicationDetector = new(config.CurrentValue.RequestJournal);
        clientHandlerName = config.CurrentValue.ClientHandlerName;
        protocolVersion = config.CurrentValue.ProtocolVersion;
        protocolVersionPolicy = config.CurrentValue.ProtocolVersionPolicy;
        this.localNode = localNode;
        localNodeId = ClusterMemberId.FromEndPoint(localNode);
        ProtocolPath = new(localNode.Uri.GetComponents(UriComponents.Path, UriFormat.Unescaped) is { Length: > 0 } protocolPath
            ? string.Concat("/", protocolPath)
            : RaftClusterMember.DefaultProtocolPath);
        coldStart = config.CurrentValue.ColdStart;
        warmupRounds = config.CurrentValue.WarmupRounds;
        messageHandlers = [];

        if (raftRpcTimeout > requestTimeout)
            throw new RaftProtocolException(ExceptionMessages.InvalidRpcTimeout);

        // dependencies
        Logger = logger;

        // track changes in configuration, do not track membership
        configurationTracker = config.OnChange(ConfigurationChanged);
        configurationEvents = Channel.CreateUnbounded<IClusterConfiguration<UriEndPoint>>(new() { SingleWriter = true, SingleReader = true });
    }

    private IClusterMemberLifetime? Configurator
    {
        init => configurator = value;
    }

    private IEnumerable<IInputChannel> MessageHandlers
    {
        init => messageHandlers = ImmutableList.CreateRange(value);
    }

    private IHttpMessageHandlerFactory? HttpHandler
    {
        init => httpHandlerFactory = value;
    }

    private ClusterMemberAnnouncer<UriEndPoint>? Announcer
    {
        init => announcer = value;
    }

    public static RaftHttpCluster Create(IServiceProvider provider)
    {
        var auditTrail = provider.GetRequiredService<IPersistentState>();
        var configStorage = provider.GetRequiredService<IClusterConfigurationStorage<UriEndPoint>>();
        IRaftCluster.SetConfigurationStorage(auditTrail, configStorage);

        return new(
            provider.GetRequiredService<IOptionsMonitor<HttpClusterMemberConfiguration>>(),
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<RaftHttpCluster>())
        {
            AuditTrail = auditTrail,
            Announcer = provider.GetService<ClusterMemberAnnouncer<UriEndPoint>>(),
            HttpHandler = provider.GetService<IHttpMessageHandlerFactory>(),
            MessageHandlers = provider.GetServices<IInputChannel>(),
            Configurator = provider.GetService<IClusterMemberLifetime>(),
            FailureDetectorFactory = provider.GetService<Func<TimeSpan, IRaftClusterMember, IFailureDetector>>(),
        };
    }

    private static TagList GetMeasurementTags(HttpClusterMemberConfiguration config, out UriEndPoint localNode)
    {
        localNode = new(config.PublicEndPoint ?? throw new RaftProtocolException(ExceptionMessages.UnknownLocalNodeAddress));
        return new()
        {
            { IRaftCluster.LocalAddressMeterAttributeName, localNode.ToString() },
        };
    }

    private IClusterConfigurationStorage<UriEndPoint> ConfigurationStorage
    {
        get
        {
            var storage = AuditTrail.ConfigurationStorage as IClusterConfigurationStorage<UriEndPoint>;
            Debug.Assert(storage is not null);

            return storage;
        }
    }

    /// <inheritdoc />
    IReadOnlyCollection<ISubscriber> IMessageBus.Members => Members;

    private RaftClusterMember CreateMember(UriEndPoint address)
    {
        var result = new RaftClusterMember(this, address)
        {
            Timeout = requestTimeout,
        };

        result.DefaultRequestHeaders.ConnectionClose = openConnectionForEachRequest;
        result.DefaultVersionPolicy = protocolVersionPolicy;
        result.SetProtocolVersion(protocolVersion);
        return result;
    }

    internal PathString ProtocolPath { get; }

    protected override ILogger Logger { get; }

    ILogger IHostingContext.Logger => Logger;

    ISubscriber? IMessageBus.Leader => Leader;

    private void ConfigurationChanged(HttpClusterMemberConfiguration configuration, string? name)
        => metadata = new(configuration.Metadata);

    IReadOnlyDictionary<string, string> IHostingContext.Metadata => metadata;

    bool IHostingContext.IsLeader(IRaftClusterMember member) => ReferenceEquals(Leader, member);

    public ref readonly ClusterMemberId LocalMemberId => ref localNodeId;

    HttpMessageHandler IHostingContext.CreateHttpHandler()
        => httpHandlerFactory?.CreateHandler(clientHandlerName) ?? new SocketsHttpHandler { ConnectTimeout = connectTimeout };

    bool IHostingContext.UseEfficientTransferOfLogEntries => AuditTrail.IsLogEntryLengthAlwaysPresented;

    Uri IRaftHttpCluster.LocalMemberAddress => localNode.Uri;

    public override async Task StartAsync(CancellationToken token = default)
    {
        configurator?.OnStart(this, metadata);

        var config = await ConfigurationStorage.LoadConfigurationAsync(token).ConfigureAwait(false);
        var announcementNeeded = true;
        if (coldStart && config.Members.Count is 0)
        {
            // in case of cold start, add the local member to the configuration
            config = config.Add(localNode);
            await ConfigurationStorage.SaveConfigurationAsync(config, configurationVersion: 0L, token).ConfigureAwait(false);
            announcementNeeded = false;
        }

        await ApplyConfigurationAsync(config, token).ConfigureAwait(false);
        ConfigurationStorage.ConfigurationChanged += configurationEvents.Writer.WriteAsync;
        pollingLoopTask = ConfigurationPollingLoop();
        await base.StartAsync(token).ConfigureAwait(false);
        StartFollowing();

        if (announcementNeeded && announcer is not null)
            await announcer(localNode, metadata, token).ConfigureAwait(false);
    }

    public override Task StopAsync(CancellationToken token = default)
    {
        return LifecycleToken.IsCancellationRequested ? Task.CompletedTask : StopAsync();

        async Task StopAsync()
        {
            try
            {
                configurator?.OnStop(this);
                duplicationDetector.Trim(100);
                ConfigurationStorage.ConfigurationChanged -= configurationEvents.Writer.WriteAsync;
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

    protected override ValueTask UnavailableMemberDetected(RaftClusterMember member, long term, CancellationToken token)
        => UnavailableMemberDetected(ConfigurationStorage, GetAddress(member), term, token);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            configurationTracker?.Dispose();
            duplicationDetector.Dispose();
            messageHandlers = ImmutableList<IInputChannel>.Empty;
            configurationEvents.Writer.TryComplete(CreateException());
        }

        base.Dispose(disposing);
    }
}