using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using IClientMetricsCollector = Metrics.IClientMetricsCollector;
    using Timestamp = Diagnostics.Timestamp;

    /// <summary>
    /// Represents Raft cluster member that is relies on exchange-based
    /// transport mechanism.
    /// </summary>
    internal sealed class ExchangePeer : RaftClusterMember
    {
        private readonly IClient client;
        private readonly PipeOptions pipeConfig;
        private readonly TimeSpan requestTimeout;

        internal ExchangePeer(ILocalMember localMember, IPEndPoint address, Func<IPEndPoint, IClient> clientFactory, TimeSpan requestTimeout, PipeOptions pipeConfig, IClientMetricsCollector? metrics)
            : base(localMember, address, metrics)
        {
            client = clientFactory(address);
            this.requestTimeout = requestTimeout;
            this.pipeConfig = pipeConfig;
        }

        public override ValueTask CancelPendingRequestsAsync() => client.CancelPendingRequestsAsync();

        private async Task<TResult> SendAsync<TResult, TExchange>(TExchange exchange, CancellationToken token)
            where TExchange : class, IClientExchange<TResult>
        {
            ThrowIfDisposed();
            exchange.MyPort = (ushort)LocalPort;
            var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutSource.CancelAfter(requestTimeout);
            var timeStamp = Timestamp.Current;
            try
            {
                client.Enqueue(exchange, timeoutSource.Token);
                return await exchange.Task.ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OperationCanceledException || !token.IsCancellationRequested)
            {
                Logger.MemberUnavailable(EndPoint, e);
                ChangeStatus(ClusterMemberStatus.Unavailable);
                throw new MemberUnavailableException(this, ExceptionMessages.UnavailableMember, e);
            }
            finally
            {
                metrics?.ReportResponseTime(timeStamp.Elapsed);
                timeoutSource.Dispose();
                if (exchange is IAsyncDisposable disposable)
                    await disposable.DisposeAsync().ConfigureAwait(false);
            }
        }

        private protected override Task<Result<bool>> VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
            => SendAsync<Result<bool>, VoteExchange>(new VoteExchange(term, lastLogIndex, lastLogTerm), token);

        private protected override Task<Result<bool>> PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
            => SendAsync<Result<bool>, PreVoteExchange>(new PreVoteExchange(term, lastLogIndex, lastLogTerm), token);

        private protected override Task<Result<bool>> AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
            => entries.Count > 0 ?
            SendAsync<Result<bool>, EntriesExchange>(new EntriesExchange<TEntry, TList>(term, entries, prevLogIndex, prevLogTerm, commitIndex, pipeConfig), token)
            : SendAsync<Result<bool>, HeartbeatExchange>(new HeartbeatExchange(term, prevLogIndex, prevLogTerm, commitIndex), token);

        private protected override Task<Result<bool>> InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
            => SendAsync<Result<bool>, SnapshotExchange>(new SnapshotExchange(term, snapshot, snapshotIndex, pipeConfig), token);

        private protected override Task<bool> ResignAsync(CancellationToken token)
            => SendAsync<bool, ResignExchange>(new ResignExchange(), token);

        private protected override Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(CancellationToken token)
            => SendAsync<IReadOnlyDictionary<string, string>, MetadataExchange>(new MetadataExchange(token, pipeConfig), token);

        /// <summary>
        /// Releases all resources associated with this cluster member.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> if called from <see cref="Disposable.Dispose()"/>; <see langword="false"/> if called from finalizer <see cref="Disposable.Finalize()"/>.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                client.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}