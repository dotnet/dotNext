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
        private readonly ConcurrentBag<ServerExchange> exchangePool;
        private readonly ImmutableDictionary<string, string> metadata;
        private readonly IPEndPoint hostEndPoint, publicEndPoint;
        private readonly Func<IPEndPoint, IClient> clientFactory;
        private readonly IServer server;
        private readonly PipeOptions pipeConfig;
        private readonly int exchangePoolSize;

        /// <summary>
        /// Initializes a new default implementation of Raft-based cluster.
        /// </summary>
        /// <param name="configuration">The configuration of the cluster.</param>
        public RaftCluster(Configuration configuration)
            : base(configuration, out var members)
        {
            hostEndPoint = configuration.HostEndPoint;
            publicEndPoint = configuration.PublicEndPoint;
            metadata = ImmutableDictionary.CreateRange(StringComparer.Ordinal, configuration.Metadata);
            clientFactory = configuration.CreateClient;
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

        public override Task StartAsync(CancellationToken token)
        {
            if(FindMember(publicEndPoint.Represents) is null)
                throw new RaftProtocolException(ExceptionMessages.UnresolvedLocalMember);
            //start each client
            foreach(var member in Members)
                member.Start();
            return base.StartAsync(token);
        }

        private RaftClusterMember CreateClient(IPEndPoint address, bool startClient)
        {
            var result = new RaftClusterMember(this, address, clientFactory, TimeSpan.FromMilliseconds(electionTimeoutProvider.UpperValue), pipeConfig, Metrics as IClientMetricsCollector);
            if(startClient)
                result.Start();
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
        protected RaftClusterMember CreateClient(IPEndPoint address)
            => CreateClient(address, true);

        bool ILocalMember.IsLeader(IRaftClusterMember member) => ReferenceEquals(Leader, member);

        IPEndPoint ILocalMember.Address => publicEndPoint;

        IReadOnlyDictionary<string, string> ILocalMember.Metadata => metadata;

        Task<bool> ILocalMember.ResignAsync(CancellationToken token)
            => ReceiveResignAsync(token);
        
        Task<Result<bool>> ILocalMember.ReceiveEntriesAsync<TEntry>(EndPoint sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
        {
            var member = FindMember(sender.Represents);
            return member is null ? Task.FromResult(new Result<bool>(Term, false)) : ReceiveEntriesAsync(member, senderTerm, entries, prevLogIndex, prevLogTerm, commitIndex, token);
        }
        
        Task<Result<bool>> ILocalMember.ReceiveVoteAsync(EndPoint sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        {
            var member = FindMember(sender.Represents);
            return member is null ? Task.FromResult(new Result<bool>(Term, false)) : ReceiveVoteAsync(member, term, lastLogIndex, lastLogTerm, token);
        }

        Task<Result<bool>> ILocalMember.ReceiveSnapshotAsync<TSnapshot>(EndPoint sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
        {
            var member = FindMember(sender.Represents);
            return member is null ? Task.FromResult(new Result<bool>(Term, false)) : ReceiveSnapshotAsync(member, senderTerm, snapshot, snapshotIndex, token);
        }
    }
}