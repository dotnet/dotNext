using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;
    using Net.Http;
    using IClientMetricsCollector = Metrics.IClientMetricsCollector;
    using HttpProtocolVersion = Net.Http.HttpProtocolVersion;

    internal abstract partial class RaftHttpCluster : RaftCluster<RaftClusterMember>, IHostedService, IHostingContext, IExpandableCluster, IMessageBus
    {
        private static readonly Func<RaftProtocolException> UnresolvedLocalMemberExceptionFactory = CreateUnresolvedLocalMemberException;
        private readonly IClusterMemberLifetime? configurator;
        private readonly IDisposable configurationTracker;
        private readonly IHttpMessageHandlerFactory? httpHandlerFactory;
        private readonly TimeSpan requestTimeout, raftRpcTimeout, connectTimeout;
        private readonly bool openConnectionForEachRequest;
        private readonly string clientHandlerName;
        private readonly HttpProtocolVersion protocolVersion;
        private readonly HttpVersionPolicy protocolVersionPolicy;
        private readonly RaftLogEntriesBufferingOptions? bufferingOptions;
        private Optional<ClusterMemberId> localMember;

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
            protocolVersionPolicy = config.ProtocolVersionPolicy;

            // dependencies
            configurator = dependencies.GetService<IClusterMemberLifetime>();
            messageHandlers = ImmutableList.CreateRange(dependencies.GetServices<IInputChannel>());
            AuditTrail = dependencies.GetService<IPersistentState>() ?? new ConsensusOnlyState();
            httpHandlerFactory = dependencies.GetService<IHttpMessageHandlerFactory>();
            Logger = dependencies.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
            Metrics = dependencies.GetService<MetricsCollector>();
            bufferingOptions = dependencies.GetBufferingOptions();
            discoveryService = dependencies.GetService<IMemberDiscoveryService>();

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

        private static RaftProtocolException CreateUnresolvedLocalMemberException()
            => new(ExceptionMessages.UnresolvedLocalMember);

        private protected void ConfigureMember(RaftClusterMember member)
        {
            member.Timeout = requestTimeout;
            member.DefaultRequestHeaders.ConnectionClose = openConnectionForEachRequest;
            member.DefaultVersionPolicy = protocolVersionPolicy;
            member.Metrics = Metrics as IClientMetricsCollector;
            member.SetProtocolVersion(protocolVersion);
        }

        private protected abstract RaftClusterMember CreateMember(Uri address);

        protected override ILogger Logger { get; }

        ILogger IHostingContext.Logger => Logger;

        IReadOnlyCollection<ISubscriber> IMessageBus.Members => Members;

        ISubscriber? IMessageBus.Leader => Leader;

        private void ConfigurationChanged(HttpClusterMemberConfiguration configuration, string name)
        {
            metadata = new MemberMetadata(configuration.Metadata);
            allowedNetworks = configuration.AllowedNetworks.ToImmutableHashSet();
        }

        private async void ConfigurationAndMembershipChanged(HttpClusterMemberConfiguration configuration, string name)
        {
            ConfigurationChanged(configuration, name);
            await ChangeMembersAsync(ChangeMembers, configuration.Members, CancellationToken.None).ConfigureAwait(false);
        }

        IReadOnlyDictionary<string, string> IHostingContext.Metadata => metadata;

        bool IHostingContext.IsLeader(IRaftClusterMember member) => ReferenceEquals(Leader, member);

        ref readonly ClusterMemberId IHostingContext.LocalEndpoint
            => ref localMember.GetReference(UnresolvedLocalMemberExceptionFactory);

        HttpMessageHandler IHostingContext.CreateHttpHandler()
            => httpHandlerFactory?.CreateHandler(clientHandlerName) ?? new SocketsHttpHandler { ConnectTimeout = connectTimeout };

        bool IHostingContext.UseEfficientTransferOfLogEntries => AuditTrail.IsLogEntryLengthAlwaysPresented;

        public event ClusterChangedEventHandler? MemberAdded;

        public event ClusterChangedEventHandler? MemberRemoved;

        public override async Task StartAsync(CancellationToken token)
        {
            if (raftRpcTimeout > requestTimeout)
                throw new RaftProtocolException(ExceptionMessages.InvalidRpcTimeout);

            // discover members
            if (discoveryService is not null)
                await DiscoverMembersAsync(discoveryService, token).ConfigureAwait(false);

            // detect local member
            localMember = await DetectLocalMemberAsync(token).ConfigureAwait(false);
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
                localMember = default;
                configurationTracker.Dispose();
                duplicationDetector.Dispose();
                messageHandlers = ImmutableList<IInputChannel>.Empty;
            }

            base.Dispose(disposing);
        }
    }
}
