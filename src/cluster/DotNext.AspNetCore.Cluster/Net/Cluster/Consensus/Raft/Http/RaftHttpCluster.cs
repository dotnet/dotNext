using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
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
        private readonly HttpEndPoint localNode;
        private readonly Uri protocolPath;
        private readonly int warmupRounds;

        // TODO: Replace IServiceProvider with nullable parameters to use optional dependency injection
        private RaftHttpCluster(HttpClusterMemberConfiguration config, IServiceProvider dependencies, Func<Action<HttpClusterMemberConfiguration, string>, IDisposable> configTracker)
            : base(config)
        {
            openConnectionForEachRequest = config.OpenConnectionForEachRequest;
            metadata = new MemberMetadata(config.Metadata);
            requestTimeout = config.RequestTimeout;
            raftRpcTimeout = config.RpcTimeout;
            connectTimeout = TimeSpan.FromMilliseconds(config.LowerElectionTimeout);
            duplicationDetector = new DuplicateRequestDetector(config.RequestJournal);
            clientHandlerName = config.ClientHandlerName;
            protocolVersion = config.ProtocolVersion;
            protocolVersionPolicy = config.ProtocolVersionPolicy;
            localNode = config.PublicEndPoint ?? throw new RaftProtocolException(ExceptionMessages.UnknownLocalNodeAddress);
            protocolPath = new Uri(config.ProtocolPath.Value.IfNullOrEmpty(HttpClusterMemberConfiguration.DefaultResourcePath), UriKind.Relative);
            coldStart = config.ColdStart;
            warmupRounds = config.WarmupRounds;

            if (raftRpcTimeout > requestTimeout)
                throw new RaftProtocolException(ExceptionMessages.InvalidRpcTimeout);

            // dependencies
            configurator = dependencies.GetService<IClusterMemberLifetime>();
            messageHandlers = ImmutableList.CreateRange(dependencies.GetServices<IInputChannel>());
            AuditTrail = dependencies.GetService<IPersistentState>() ?? new ConsensusOnlyState();
            ConfigurationStorage = dependencies.GetService<IClusterConfigurationStorage<HttpEndPoint>>() ?? new InMemoryClusterConfigurationStorage();
            httpHandlerFactory = dependencies.GetService<IHttpMessageHandlerFactory>();
            Logger = dependencies.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
            Metrics = dependencies.GetService<MetricsCollector>();
            announcer = dependencies.GetService<ClusterMemberAnnouncer<HttpEndPoint>>();

            // track changes in configuration, do not track membership if discovery service is enabled
            configurationTracker = configTracker(ConfigurationChanged);

            pollingLoopTask = Task.CompletedTask;
        }

        private RaftHttpCluster(IOptionsMonitor<HttpClusterMemberConfiguration> config, IServiceProvider dependencies)
            : this(config.CurrentValue, dependencies, config.OnChange)
        {
        }

        public RaftHttpCluster(IServiceProvider dependencies)
            : this(dependencies.GetRequiredService<IOptionsMonitor<HttpClusterMemberConfiguration>>(), dependencies)
        {
        }

        protected override IClusterConfigurationStorage<HttpEndPoint> ConfigurationStorage { get; }

        private RaftClusterMember CreateMember(in ClusterMemberId id, HttpEndPoint address)
        {
            var result = new RaftClusterMember(this, address, protocolPath, id)
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

        internal PathString ProtocolPath => protocolPath.OriginalString;

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

        HttpEndPoint IRaftHttpCluster.LocalMemberAddress => localNode;

        public override async Task StartAsync(CancellationToken token)
        {
            configurator?.OnStart(this, metadata);
            pollingLoopTask = ConfigurationPollingLoop();

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
            }

            await base.StartAsync(token).ConfigureAwait(false);

            if (!coldStart && announcer is not null)
                await announcer(LocalMemberId, localNode, token).ConfigureAwait(false);
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
}
