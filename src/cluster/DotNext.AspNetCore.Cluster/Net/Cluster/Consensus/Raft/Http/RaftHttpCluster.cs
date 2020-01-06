using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using DistributedServices;
    using Messaging;
    using Threading;

    internal abstract partial class RaftHttpCluster : RaftCluster<RaftClusterMember>, IHostedService, IHostingContext, IExpandableCluster, IDistributedApplicationEnvironment
    {
        private readonly IClusterMemberLifetime configurator;
        private volatile ImmutableList<IMessageHandler> messageHandlers;

        private readonly IDisposable configurationTracker;
        private volatile MemberMetadata metadata;
        private volatile ISet<IPNetwork> allowedNetworks;

        [SuppressMessage("Usage", "CA2213", Justification = "This object is disposed via RaftCluster.members collection")]
        private RaftClusterMember? localMember;

        private readonly IHttpMessageHandlerFactory httpHandlerFactory;
        private readonly TimeSpan requestTimeout;
        private readonly DuplicateRequestDetector duplicationDetector;
        private readonly bool openConnectionForEachRequest;
        private readonly string clientHandlerName;
        private readonly HttpVersion protocolVersion;
        //distributed services
        private IDistributedLockProvider? distributedLock;

        [SuppressMessage("Reliability", "CA2000", Justification = "The member will be disposed in RaftCluster.Dispose method")]
        private RaftHttpCluster(RaftClusterMemberConfiguration config, IServiceProvider dependencies, out MutableMemberCollection members, Func<Action<RaftClusterMemberConfiguration, string>, IDisposable> configTracker)
            : base(config, out members)
        {
            openConnectionForEachRequest = config.OpenConnectionForEachRequest;
            allowedNetworks = config.AllowedNetworks;
            metadata = new MemberMetadata(config.Metadata);
            requestTimeout = TimeSpan.FromMilliseconds(config.UpperElectionTimeout);
            duplicationDetector = new DuplicateRequestDetector(config.RequestJournal);
            clientHandlerName = config.ClientHandlerName;
            protocolVersion = config.ProtocolVersion;
            //dependencies
            configurator = dependencies.GetService<IClusterMemberLifetime>();
            messageHandlers = ImmutableList.CreateRange(dependencies.GetServices<IMessageHandler>());
            AuditTrail = dependencies.GetService<IPersistentState>() ?? new InMemoryAuditTrail();
            httpHandlerFactory = dependencies.GetService<IHttpMessageHandlerFactory>();
            Logger = dependencies.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
            Metrics = dependencies.GetService<MetricsCollector>();
            //track changes in configuration
            configurationTracker = configTracker(ConfigurationChanged);
        }

        private RaftHttpCluster(IOptionsMonitor<RaftClusterMemberConfiguration> config, IServiceProvider dependencies, out MutableMemberCollection members)
            : this(config.CurrentValue, dependencies, out members, config.OnChange)
        {
        }

        private protected RaftHttpCluster(IServiceProvider dependencies, out MutableMemberCollection members)
            : this(dependencies.GetRequiredService<IOptionsMonitor<RaftClusterMemberConfiguration>>(), dependencies, out members)
        {
        }

        internal bool IsDistributedServicesSupported => distributedLock != null;

        IDistributedLockProvider IDistributedApplicationEnvironment.LockProvider => distributedLock ?? throw new NotSupportedException(ExceptionMessages.DistributedServicesAreUnavailable);

        private protected void ConfigureMember(RaftClusterMember member)
        {
            member.Timeout = requestTimeout;
            member.DefaultRequestHeaders.ConnectionClose = openConnectionForEachRequest;
            member.Metrics = Metrics as IHttpClientMetrics;
            member.ProtocolVersion = protocolVersion;
        }

        private protected abstract RaftClusterMember CreateMember(Uri address);

        protected override ILogger Logger { get; }

        ILogger IHostingContext.Logger => Logger;

        IReadOnlyCollection<ISubscriber> IMessageBus.Members => Members;

        ISubscriber? IMessageBus.Leader => Leader;

        private async void ConfigurationChanged(RaftClusterMemberConfiguration configuration, string name)
        {
            metadata = new MemberMetadata(configuration.Metadata);
            allowedNetworks = configuration.AllowedNetworks;
            await ChangeMembersAsync(members =>
            {
                var existingMembers = new HashSet<Uri>();
                //remove members
                foreach (var holder in members)
                    if (configuration.Members.Contains(holder.Member.BaseAddress))
                        existingMembers.Add(holder.Member.BaseAddress);
                    else
                    {
                        var member = holder.Remove();
                        MemberRemoved?.Invoke(this, member);
                        member.CancelPendingRequests();
                    }

                //add new members
                foreach (var memberUri in configuration.Members)
                    if (!existingMembers.Contains(memberUri))
                    {
                        var member = CreateMember(memberUri);
                        members.Add(member);
                        MemberAdded?.Invoke(this, member);
                    }

                existingMembers.Clear();
            }).ConfigureAwait(false);
        }

        IReadOnlyDictionary<string, string> IHostingContext.Metadata => metadata;

        bool IHostingContext.IsLeader(IRaftClusterMember member) => ReferenceEquals(Leader, member);

        IPEndPoint IHostingContext.LocalEndpoint => localMember?.Endpoint ?? throw new RaftProtocolException(ExceptionMessages.UnresolvedLocalMember);

        HttpMessageHandler IHostingContext.CreateHttpHandler()
            => httpHandlerFactory?.CreateHandler(clientHandlerName) ?? new HttpClientHandler();

        public event ClusterChangedEventHandler? MemberAdded;
        public event ClusterChangedEventHandler? MemberRemoved;

        private protected abstract Predicate<RaftClusterMember> LocalMemberFinder { get; }

        public override Task StartAsync(CancellationToken token)
        {
            //detect local member
            localMember = FindMember(LocalMemberFinder);
            if (localMember is null)
                throw new RaftProtocolException(ExceptionMessages.UnresolvedLocalMember);
            configurator?.Initialize(this, metadata);
            distributedLock = DistributedLockProvider.TryCreate(this);
            return base.StartAsync(token);
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
                distributedLock = null;
                messageHandlers = ImmutableList<IMessageHandler>.Empty;
            }

            base.Dispose(disposing);
        }
    }
}
