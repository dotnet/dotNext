using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;
    using TransportServices;
    using ILocalMember = TransportServices.ILocalMember;
    using IClientMetricsCollector = Metrics.IClientMetricsCollector;

    /// <summary>
    /// Represents default implementation of Raft-based cluster.
    /// </summary>
    public partial class RaftCluster : RaftCluster<RaftClusterMember>, ILocalMember, IExchangePool
    {
        private static readonly Func<RaftClusterMember, EndPoint, bool> MatchByEndPoint = IsMatchedByEndPoint;
        
        private readonly ConcurrentBag<ServerExchange> exchangePool;
        private readonly ImmutableDictionary<string, string> metadata;
        private readonly IPEndPoint publicEndPoint;
        private readonly Func<IPEndPoint, IClient> clientFactory;
        private readonly Func<IServer> serverFactory;
        private IServer server;
        private readonly PipeOptions pipeConfig;

        /// <summary>
        /// Initializes a new default implementation of Raft-based cluster.
        /// </summary>
        /// <param name="configuration">The configuration of the cluster.</param>
        public RaftCluster(Configuration configuration)
            : base(configuration, out var members)
        {
            publicEndPoint = configuration.PublicEndPoint;
            metadata = ImmutableDictionary.CreateRange(StringComparer.Ordinal, configuration.Metadata);
            clientFactory = configuration.CreateClient;
            serverFactory = configuration.CreateServer;
            pipeConfig = configuration.PipeConfig;
            server = configuration.CreateServer();
            exchangePool = new ConcurrentBag<ServerExchange>();
            //populate pool
            for(var i = 0; i <= configuration.ServerBacklog; i++)
                exchangePool.Add(new ServerExchange(this, configuration.PipeConfig));
            //create members without starting clients
            foreach(var member in configuration.Members)
                members.Add(CreateClient(member, false));
        }

        bool IExchangePool.TryRent(PacketHeaders headers, [NotNullWhen(true)] out IExchange exchange)
        {
            var result = exchangePool.TryTake(out var serverExchange);
            exchange = serverExchange;
            return result;
        }

        void IExchangePool.Release(IExchange exchange)
        {
            if(exchange is ServerExchange serverExchange)
            {
                serverExchange.Reset();
                exchangePool.Add(serverExchange);
            }
        }

        private static bool IsMatchedByEndPoint(RaftClusterMember member, EndPoint endPoint)
            => member.Endpoint.Equals(endPoint);

        /// <summary>
        /// Starts serving local member.
        /// </summary>
        /// <param name="token">The token that can be used to cancel initialization process.</param>
        /// <returns>The task representing asynchronous execution of the method.</returns>
        public override Task StartAsync(CancellationToken token)
        {
            if(FindMember(MatchByEndPoint, publicEndPoint) is null)
                throw new RaftProtocolException(ExceptionMessages.UnresolvedLocalMember);
            server = serverFactory();
            server.Start(this);
            return base.StartAsync(token);
        }

        /// <summary>
        /// Stops serving local member.
        /// </summary>
        /// <param name="token">The token that can be used to cancel shutdown process.</param>
        /// <returns>The task representing asynchronous execution of the method.</returns>
        public override Task StopAsync(CancellationToken token)
        {
            server.Dispose();
            return base.StopAsync(token);
        }

        private RaftClusterMember CreateClient(IPEndPoint address, bool extendPool)
        {
            var result = new RaftClusterMember(this, address, clientFactory, TimeSpan.FromMilliseconds(electionTimeoutProvider.UpperValue), pipeConfig, Metrics as IClientMetricsCollector);
            if(extendPool)
                exchangePool.Add(new ServerExchange(this, pipeConfig));
            return result;
        }

        /// <summary>
        /// Initializes a new client for communication with cluster member.
        /// </summary>
        /// <remarks>
        /// This method is needed if you want to implement dynamic addition of the new cluster members.
        /// </remarks>
        /// <param name="address">The address of the cluster member.</param>
        /// <returns>A new client for communication with cluster member.</returns>
        protected RaftClusterMember CreateClient(IPEndPoint address) => CreateClient(address, true);

        /// <summary>
        /// Called automatically when member is removed from the collection of members.
        /// </summary>
        /// <param name="member">The removed member.</param>
        protected sealed override void OnRemoved(RaftClusterMember member)
        {
            if(exchangePool.TryTake(out var exchange))
                exchange.Dispose();
        }

        bool ILocalMember.IsLeader(IRaftClusterMember member) => ReferenceEquals(Leader, member);

        IPEndPoint ILocalMember.Address => publicEndPoint;

        IReadOnlyDictionary<string, string> ILocalMember.Metadata => metadata;

        Task<bool> ILocalMember.ResignAsync(CancellationToken token)
            => ReceiveResignAsync(token);
        
        Task<Result<bool>> ILocalMember.ReceiveEntriesAsync<TEntry>(EndPoint sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
        {
            var member = FindMember(MatchByEndPoint, sender);
            return member is null ? Task.FromResult(new Result<bool>(Term, false)) : ReceiveEntriesAsync(member, senderTerm, entries, prevLogIndex, prevLogTerm, commitIndex, token);
        }
        
        Task<Result<bool>> ILocalMember.ReceiveVoteAsync(EndPoint sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        {
            var member = FindMember(MatchByEndPoint, sender);
            return member is null ? Task.FromResult(new Result<bool>(Term, false)) : ReceiveVoteAsync(member, term, lastLogIndex, lastLogTerm, token);
        }

        Task<Result<bool>> ILocalMember.ReceiveSnapshotAsync<TSnapshot>(EndPoint sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
        {
            var member = FindMember(MatchByEndPoint, sender);
            return member is null ? Task.FromResult(new Result<bool>(Term, false)) : ReceiveSnapshotAsync(member, senderTerm, snapshot, snapshotIndex, token);
        }

        /// <summary>
        /// Releases managed and unmanaged resources associated with this object.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> if called from <see cref="Disposable.Dispose()"/>; <see langword="false"/> if called from finalizer <see cref="Disposable.Finalize()"/>.</param>
        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                server.Dispose();
                //dispose all exchanges
                while(exchangePool.TryTake(out var exchange))
                    exchange.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}