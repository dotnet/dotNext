using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;
    using Net.Http;
    using HttpProtocolVersion = Net.Http.HttpProtocolVersion;
    using IClientMetricsCollector = Metrics.IClientMetricsCollector;

    internal abstract partial class RaftHttpCluster : RaftCluster<RaftClusterMember>, IHostedService, IHostingContext, IMessageBus
    {
        private readonly IClusterMemberLifetime? configurator;
        private readonly IDisposable configurationTracker;
        private readonly IHttpMessageHandlerFactory? httpHandlerFactory;
        private readonly TimeSpan requestTimeout, raftRpcTimeout, connectTimeout;
        private readonly bool openConnectionForEachRequest;
        private readonly string clientHandlerName;
        private readonly HttpProtocolVersion protocolVersion;
#if !NETCOREAPP3_1
        private readonly HttpVersionPolicy protocolVersionPolicy;
#endif
        private readonly RaftLogEntriesBufferingOptions? bufferingOptions;
        private Uri? localNode; // TODO: Must be non-nullable and readonly in .NEXT 4

        // TODO: Replace IServiceProvider with nullable parameters to use optional dependency injection
        private RaftHttpCluster(HttpClusterMemberConfiguration config, IServiceProvider dependencies, out MemberCollectionBuilder members, Func<Action<HttpClusterMemberConfiguration, string>, IDisposable> configTracker)
            : base(config, out members)
        {
            openConnectionForEachRequest = config.OpenConnectionForEachRequest;
            allowedNetworks = config.AllowedNetworks.ToImmutableHashSet();
            metadata = new MemberMetadata(config.Metadata);
            requestTimeout = config.RequestTimeout;
            raftRpcTimeout = config.RpcTimeout;
            connectTimeout = TimeSpan.FromMilliseconds(config.LowerElectionTimeout);
            duplicationDetector = new DuplicateRequestDetector(config.RequestJournal);
            clientHandlerName = config.ClientHandlerName;
            protocolVersion = config.ProtocolVersion;
#if !NETCOREAPP3_1
            protocolVersionPolicy = config.ProtocolVersionPolicy;
#endif
            localNode = config.PublicEndPoint;

            if (localNode is null && bootstrapMode != ClusterMemberBootstrap.Recovery)
                throw new ArgumentException(ExceptionMessages.UnknownLocalNodeAddress, nameof(config));

            if (raftRpcTimeout > requestTimeout)
                throw new RaftProtocolException(ExceptionMessages.InvalidRpcTimeout);

            // dependencies
            configurator = dependencies.GetService<IClusterMemberLifetime>();
            messageHandlers = ImmutableList.CreateRange(dependencies.GetServices<IInputChannel>());
            AuditTrail = dependencies.GetService<IPersistentState>() ?? new ConsensusOnlyState();
            httpHandlerFactory = dependencies.GetService<IHttpMessageHandlerFactory>();
            Logger = dependencies.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
            Metrics = dependencies.GetService<MetricsCollector>();
            bufferingOptions = dependencies.GetBufferingOptions();
            discoveryService = dependencies.GetService<IMemberDiscoveryService>();
            announcer = dependencies.GetService<ClusterMemberAnnouncer<Uri>>();

            // track changes in configuration, do not track membership if discovery service is enabled
            configurationTracker = configTracker(discoveryService is null ? ConfigurationAndMembershipChanged : ConfigurationChanged);
        }

        private RaftHttpCluster(IOptionsMonitor<HttpClusterMemberConfiguration> config, IServiceProvider dependencies, out MemberCollectionBuilder members)
            : this(config.CurrentValue, dependencies, out members, config.OnChange)
        {
        }

        private protected RaftHttpCluster(IServiceProvider dependencies, out MemberCollectionBuilder members)
            : this(dependencies.GetRequiredService<IOptionsMonitor<HttpClusterMemberConfiguration>>(), dependencies, out members)
        {
        }

        private protected void ConfigureMember(RaftClusterMember member)
        {
            member.Timeout = requestTimeout;
            member.DefaultRequestHeaders.ConnectionClose = openConnectionForEachRequest;
#if !NETCOREAPP3_1
            member.DefaultVersionPolicy = protocolVersionPolicy;
#endif
            member.Metrics = Metrics as IClientMetricsCollector;
            member.SetProtocolVersion(protocolVersion);
        }

        private protected abstract RaftClusterMember CreateMember(Uri address, ClusterMemberId? id);

        protected sealed override RaftClusterMember CreateMember(in ClusterMemberId id, ReadOnlyMemory<byte> address)
        {
            var result = CreateMember(new(Encoding.UTF8.GetString(address.Span), UriKind.Absolute), id);
            result.IsRemote = !Equals(result.BaseAddress, localNode);
            return result;
        }

        protected sealed override ILogger Logger { get; }

        ILogger IHostingContext.Logger => Logger;

        IReadOnlyCollection<ISubscriber> IMessageBus.Members => Members;

        ISubscriber? IMessageBus.Leader => Leader;

        private void ConfigurationChanged(HttpClusterMemberConfiguration configuration, string name)
        {
            metadata = new MemberMetadata(configuration.Metadata);
            allowedNetworks = configuration.AllowedNetworks.ToImmutableHashSet();
        }

        [Obsolete]
        private async void ConfigurationAndMembershipChanged(HttpClusterMemberConfiguration configuration, string name)
        {
            ConfigurationChanged(configuration, name);
            await ChangeMembersAsync(ChangeMembers, configuration.Members, CancellationToken.None).ConfigureAwait(false);
        }

        IReadOnlyDictionary<string, string> IHostingContext.Metadata => metadata;

        bool IHostingContext.IsLeader(IRaftClusterMember member) => ReferenceEquals(Leader, member);

        ref readonly ClusterMemberId IHostingContext.LocalMember => ref LocalMemberId;

        HttpMessageHandler IHostingContext.CreateHttpHandler()
            => httpHandlerFactory?.CreateHandler(clientHandlerName) ?? new SocketsHttpHandler { ConnectTimeout = connectTimeout };

        bool IHostingContext.UseEfficientTransferOfLogEntries => AuditTrail.IsLogEntryLengthAlwaysPresented;

        public override async Task StartAsync(CancellationToken token)
        {
            if (bootstrapMode == ClusterMemberBootstrap.Recovery)
            {
                // discover members
                if (discoveryService is not null)
                    await DiscoverMembersAsync(discoveryService, token).ConfigureAwait(false);

                // detect local member
                localNode = await DetectLocalMemberAsync(token).ConfigureAwait(false) ?? throw new RaftProtocolException(ExceptionMessages.UnknownLocalNodeAddress);
            }
            else
            {
                Debug.Assert(localNode is not null);
            }

            configurator?.Initialize(this, metadata);
            await base.StartAsync(token).ConfigureAwait(false);
        }

        public override Task StopAsync(CancellationToken token)
        {
            configurator?.Shutdown(this);
            duplicationDetector.Trim(100);
            var result = base.StopAsync(token);
            if (membershipWatch is not null)
            {
                membershipWatch.Dispose();
                membershipWatch = null;
            }

            return result;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                membershipWatch?.Dispose();
                configurationTracker.Dispose();
                duplicationDetector.Dispose();
                messageHandlers = ImmutableList<IInputChannel>.Empty;
            }

            base.Dispose(disposing);
        }
    }
}
