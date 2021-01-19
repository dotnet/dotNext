using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
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
    using IClientMetricsCollector = Metrics.IClientMetricsCollector;

    internal abstract partial class RaftHttpCluster : RaftCluster<RaftClusterMember>, IHostedService, IHostingContext, IExpandableCluster, IMessageBus
    {
        private readonly IClusterMemberLifetime? configurator;
        private readonly IDisposable configurationTracker;
        private readonly IHttpMessageHandlerFactory? httpHandlerFactory;
        private readonly TimeSpan requestTimeout, raftRpcTimeout, connectTimeout;
        private readonly bool openConnectionForEachRequest;
        private readonly string clientHandlerName;
        private readonly HttpVersion protocolVersion;
        private ClusterMemberId? localMember;

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

            // dependencies
            configurator = dependencies.GetService<IClusterMemberLifetime>();
            messageHandlers = ImmutableList.CreateRange(dependencies.GetServices<IInputChannel>());
            AuditTrail = dependencies.GetService<IPersistentState>() ?? new ConsensusOnlyState();
            httpHandlerFactory = dependencies.GetService<IHttpMessageHandlerFactory>();
            Logger = dependencies.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
            Metrics = dependencies.GetService<MetricsCollector>();

            // track changes in configuration
            configurationTracker = configTracker(ConfigurationChanged);
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
            member.Metrics = Metrics as IClientMetricsCollector;
            member.ProtocolVersion = protocolVersion;
        }

        private protected abstract RaftClusterMember CreateMember(Uri address);

        protected override ILogger Logger { get; }

        ILogger IHostingContext.Logger => Logger;

        IReadOnlyCollection<ISubscriber> IMessageBus.Members => Members;

        ISubscriber? IMessageBus.Leader => Leader;

        private async void ConfigurationChanged(HttpClusterMemberConfiguration configuration, string name)
        {
            metadata = new MemberMetadata(configuration.Metadata);
            allowedNetworks = configuration.AllowedNetworks.ToImmutableHashSet();
            await ChangeMembersAsync((in MemberCollectionBuilder members) =>
            {
                var existingMembers = new HashSet<Uri>();

                // remove members
                foreach (var holder in members)
                {
                    Debug.Assert(holder.Member.BaseAddress is not null);
                    if (configuration.Members.Contains(holder.Member.BaseAddress))
                    {
                        existingMembers.Add(holder.Member.BaseAddress);
                    }
                    else
                    {
                        using var member = holder.Remove();
                        MemberRemoved?.Invoke(this, member);
                        member.CancelPendingRequests();
                    }
                }

                // add new members
                foreach (var memberUri in configuration.Members)
                {
                    if (!existingMembers.Contains(memberUri))
                    {
                        var member = CreateMember(memberUri);
                        members.Add(member);
                        MemberAdded?.Invoke(this, member);
                    }
                }

                existingMembers.Clear();
            }).ConfigureAwait(false);
        }

        IReadOnlyDictionary<string, string> IHostingContext.Metadata => metadata;

        bool IHostingContext.IsLeader(IRaftClusterMember member) => ReferenceEquals(Leader, member);

        ClusterMemberId IHostingContext.LocalEndpoint => localMember ?? throw new RaftProtocolException(ExceptionMessages.UnresolvedLocalMember);

        HttpMessageHandler IHostingContext.CreateHttpHandler()
            => httpHandlerFactory?.CreateHandler(clientHandlerName) ?? new SocketsHttpHandler { ConnectTimeout = connectTimeout };

        bool IHostingContext.UseEfficientTransferOfLogEntries => AuditTrail.IsLogEntryLengthAlwaysPresented;

        public event ClusterChangedEventHandler? MemberAdded;

        public event ClusterChangedEventHandler? MemberRemoved;

        private protected abstract Task<ICollection<EndPoint>> GetHostingAddressesAsync();

        private async Task<ClusterMemberId> DetectLocalMemberAsync()
        {
            Predicate<IRaftClusterMember>? selector = configurator?.LocalMemberSelector;
            if (selector is null)
            {
                var addresses = await GetHostingAddressesAsync().ConfigureAwait(false);
                selector = addresses.Contains;
            }

            return FindMember(selector)?.Id ?? throw new RaftProtocolException(ExceptionMessages.UnresolvedLocalMember);
        }

        public override async Task StartAsync(CancellationToken token)
        {
            if (raftRpcTimeout > requestTimeout)
                throw new RaftProtocolException(ExceptionMessages.InvalidRpcTimeout);

            // detect local member
            localMember = await DetectLocalMemberAsync().ConfigureAwait(false);
            configurator?.Initialize(this, metadata);
            await base.StartAsync(token).ConfigureAwait(false);
        }

        public override Task StopAsync(CancellationToken token)
        {
            configurator?.Shutdown(this);
            duplicationDetector.Trim(100);
            return base.StopAsync(token);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                localMember = null;
                configurationTracker.Dispose();
                duplicationDetector.Dispose();
                messageHandlers = ImmutableList<IInputChannel>.Empty;
            }

            base.Dispose(disposing);
        }
    }
}
