using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Threading;
    using TransportServices;
    using IClientMetricsCollector = Metrics.IClientMetricsCollector;
    using Timestamp = Diagnostics.Timestamp;
    
    /// <summary>
    /// Represents Raft cluster member that is accessible through the network.
    /// </summary>
    public sealed class RaftClusterMember : Disposable, IRaftClusterMember
    {
        private interface IRequest<TExchange, TResult>
            where TExchange : class, IExchange
        {
            TExchange CreateExchange();

            Task<TResult> GetResultAsync(TExchange exchange);

            ValueTask ReleaseExchange(TExchange exchange);
        }

        private long nextIndex;
        private readonly IClient client;
        private readonly IClientMetricsCollector? metrics;
        private volatile IReadOnlyDictionary<string, string>? metadataCache;
        private AtomicEnum<ClusterMemberStatus> status;
        private readonly ILocalMember localMember;
        private readonly PipeOptions pipeConfig;

        internal RaftClusterMember(ILocalMember localMember, IPEndPoint address, Func<IPEndPoint, IClient> clientFactory, TimeSpan requestTimeout, PipeOptions pipeConfig, IClientMetricsCollector? metrics)
        {
            client = clientFactory(address);
            this.metrics = metrics;
            Endpoint = address;
            status = new AtomicEnum<ClusterMemberStatus>(ClusterMemberStatus.Unknown);
            RequestTimeout = requestTimeout;
            this.localMember = localMember;
            this.pipeConfig = pipeConfig;
        }

        internal void Start() => client.Start();

        /// <summary>
        /// Gets request timeout used for communication with this member.
        /// </summary>
        /// <value></value>
        public TimeSpan RequestTimeout { get; }

        /// <summary>
        /// Gets the address of this cluster member.
        /// </summary>
        /// <value></value>
        public IPEndPoint Endpoint { get; }

        /// <summary>
        /// Determines whether this member is a leader.
        /// </summary>
        public bool IsLeader => localMember.IsLeader(this);

        /// <summary>
        /// Determines whether this member is not a local node.
        /// </summary>
        public bool IsRemote => !Endpoint.Equals(localMember.Address);

        /// <summary>
        /// Gets the status of this member.
        /// </summary>
        public ClusterMemberStatus Status => status.Value;

        /// <summary>
        /// Informs about status change.
        /// </summary>
        public event ClusterMemberStatusChanged? MemberStatusChanged;

        ref long IRaftClusterMember.NextIndex => ref nextIndex;

        void IRaftClusterMember.CancelPendingRequests() => client.CancelPendingRequests();

        private void ChangeStatus(ClusterMemberStatus newState)
            => IClusterMember.OnMemberStatusChanged(this, ref status, newState, MemberStatusChanged);

        internal void Touch() => ChangeStatus(ClusterMemberStatus.Available);

        private async Task<TResult> SendAsync<TResult, TExchange>(TExchange exchange, CancellationToken token)
            where TExchange : class, IClientExchange<TResult>
        {
            ThrowIfDisposed();
            var timeoutSource = new CancellationTokenSource(RequestTimeout);
            var linkedSource = token.LinkTo(timeoutSource.Token);
            var timeStamp = Timestamp.Current;
            try
            {
                client.Enqueue(exchange, token);
                return await exchange.Task.ConfigureAwait(false);
            }
            catch(Exception e)
            {
                ChangeStatus(ClusterMemberStatus.Unavailable);
                throw new MemberUnavailableException(this, ExceptionMessages.UnavailableMember, e);
            }
            finally
            {
                metrics?.ReportResponseTime(timeStamp.Elapsed);
                linkedSource?.Dispose();
                timeoutSource.Dispose();
                if(exchange is IAsyncDisposable disposable)
                    await disposable.DisposeAsync();
            }
        }

        Task<Result<bool>> IRaftClusterMember.VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
            => Endpoint.Equals(localMember.Address) ?
                Task.FromResult(new Result<bool>(term, true)) :
                SendAsync<Result<bool>, VoteExchange>(new VoteExchange(term, lastLogIndex, lastLogTerm), token);
        
        Task<Result<bool>> IRaftClusterMember.AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
        {
            if(Endpoint.Equals(localMember.Address))
                return Task.FromResult(new Result<bool>(term, true));
            return SendAsync<Result<bool>, EntriesExchange>(new EntriesExchange<TEntry, TList>(term, entries, prevLogIndex, prevLogTerm, commitIndex, pipeConfig), token);
        }

        Task<Result<bool>> IRaftClusterMember.InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
        {
            if(Endpoint.Equals(localMember.Address))
                return Task.FromResult(new Result<bool>(term, true));
            return SendAsync<Result<bool>, SnapshotExchange>(new SnapshotExchange(term, snapshot, snapshotIndex, pipeConfig), token);
        }

        Task<bool> IClusterMember.ResignAsync(CancellationToken token)
            => SendAsync<bool, ResignExchange>(new ResignExchange(), token);

        async ValueTask<IReadOnlyDictionary<string, string>> IClusterMember.GetMetadataAsync(bool refresh, CancellationToken token)
        {
            if(Endpoint.Equals(localMember.Address))
                return localMember.Metadata;
            if(metadataCache is null || refresh)
                metadataCache = await SendAsync<IReadOnlyDictionary<string, string>, MetadataExchange>(new MetadataExchange(token, pipeConfig), token).ConfigureAwait(false);
            return metadataCache;
        }

        /// <summary>
        /// Releases all resources associated with this cluster member.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> if called from <see cref="Disposable.Dispose()"/>; <see langword="false"/> if called from finalizer <see cref="Disposable.Finalize()"/>.</param>
        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                client.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}