using System.IO.Pipelines;
using System.Net;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using IClientMetricsCollector = Metrics.IClientMetricsCollector;
using IClusterConfiguration = Membership.IClusterConfiguration;
using Timestamp = Diagnostics.Timestamp;

/// <summary>
/// Represents Raft cluster member that is relies on exchange-based
/// transport mechanism.
/// </summary>
internal sealed class ExchangePeer : RaftClusterMember
{
    private readonly IClient client;
    private readonly PipeOptions pipeConfig;

    internal ExchangePeer(ILocalMember localMember, IPEndPoint address, ClusterMemberId id, Func<IPEndPoint, IClient> clientFactory)
        : base(localMember, address, id)
    {
        client = clientFactory(address);
        pipeConfig = PipeOptions.Default;
    }

    internal PipeOptions PipeConfig
    {
        get => pipeConfig;
        init => pipeConfig = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override ValueTask CancelPendingRequestsAsync() => client.CancelPendingRequestsAsync();

    private async Task<TResult> SendAsync<TResult, TExchange>(TExchange exchange, CancellationToken token)
        where TExchange : class, IClientExchange<Task<TResult>>
    {
        ThrowIfDisposed();
        exchange.Sender = localMember.Id;
        var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutSource.CancelAfter(RequestTimeout);
        var timeStamp = new Timestamp();
        try
        {
            client.Enqueue(exchange, timeoutSource.Token);
            return await exchange.Invoke(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException || !token.IsCancellationRequested)
        {
            Logger.MemberUnavailable(EndPoint, e);
            ChangeStatus(ClusterMemberStatus.Unavailable);
            throw new MemberUnavailableException(this, ExceptionMessages.UnavailableMember, e);
        }
        finally
        {
            Metrics?.ReportResponseTime(timeStamp.Elapsed);
            timeoutSource.Dispose();
            if (exchange is IAsyncDisposable disposable)
                await disposable.DisposeAsync().ConfigureAwait(false);
        }
    }

    private protected override Task<Result<bool>> VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        => SendAsync<Result<bool>, VoteExchange>(new VoteExchange(term, lastLogIndex, lastLogTerm), token);

    private protected override Task<Result<bool>> PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        => SendAsync<Result<bool>, PreVoteExchange>(new PreVoteExchange(term, lastLogIndex, lastLogTerm), token);

    private protected override async Task<Result<bool>> AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
    {
        EmptyClusterConfiguration? configState;
        if (config.Length is 0L)
        {
            configState = new() { Fingerprint = config.Fingerprint, ApplyConfig = applyConfig };
        }
        else
        {
            Debug.Assert(applyConfig is false);
            await SendAsync<bool, ConfigurationExchange>(new ConfigurationExchange(config, pipeConfig), token).ConfigureAwait(false);
            configState = null;
        }

        return await (entries.Count > 0
            ? SendAsync<Result<bool>, EntriesExchange>(new EntriesExchange<TEntry, TList>(term, entries, prevLogIndex, prevLogTerm, commitIndex, configState, pipeConfig), token)
            : SendAsync<Result<bool>, HeartbeatExchange>(new HeartbeatExchange(term, prevLogIndex, prevLogTerm, commitIndex, configState), token)).ConfigureAwait(false);
    }

    private protected override Task<Result<bool>> InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
        => SendAsync<Result<bool>, SnapshotExchange>(new SnapshotExchange(term, snapshot, snapshotIndex, pipeConfig), token);

    private protected override Task<bool> ResignAsync(CancellationToken token)
        => SendAsync<bool, ResignExchange>(new ResignExchange(), token);

    private protected override Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(CancellationToken token)
        => SendAsync<IReadOnlyDictionary<string, string>, MetadataExchange>(new MetadataExchange(pipeConfig), token);

    private protected override Task<long?> SynchronizeAsync(CancellationToken token)
        => SendAsync<long?, SynchronizeExchange>(new SynchronizeExchange(), token);

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