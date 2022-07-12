using System.Net;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Threading;
using Timestamp = Diagnostics.Timestamp;
using IClusterConfiguration = Membership.IClusterConfiguration;

internal abstract class Client : RaftClusterMember
{
    private protected interface IConnectionContext : IDisposable, IAsyncDisposable
    {
        ProtocolStream Protocol { get; }

        Memory<byte> Buffer { get; }
    }

    private readonly AsyncExclusiveLock accessLock;
    private readonly TimeSpan connectTimeout;
    private IConnectionContext? context;

    private protected Client(ILocalMember localMember, EndPoint endPoint, ClusterMemberId id)
        : base(localMember, endPoint, id)
    {
        accessLock = new();
        connectTimeout = TimeSpan.FromSeconds(1);
    }

    internal TimeSpan ConnectTimeout
    {
        get => connectTimeout;
        init => connectTimeout = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    private protected abstract ValueTask<IConnectionContext> ConnectAsync(CancellationToken token);

    private async Task<TResponse> RequestAsync<TResponse>(Func<ProtocolStream, Memory<byte>, CancellationToken, ValueTask<TResponse>> request, CancellationToken token)
    {
        ThrowIfDisposed();

        var timeStamp = new Timestamp();
        var lockTaken = false;

        var requestDurationTracker = CancellationTokenSource.CreateLinkedTokenSource(token);
        try
        {
            requestDurationTracker.CancelAfter(RequestTimeout);
            await accessLock.AcquireAsync(requestDurationTracker.Token).ConfigureAwait(false);
            lockTaken = true;

            context ??= await ConnectAsync(requestDurationTracker.Token).ConfigureAwait(false);

            context.Protocol.Reset();
            return await request(context.Protocol, context.Buffer, requestDurationTracker.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // canceled by caller
            context?.Dispose();
            context = null;
            throw;
        }
        catch (Exception e)
        {
            Logger.MemberUnavailable(EndPoint, e);
            ChangeStatus(ClusterMemberStatus.Unavailable);

            // detect broken socket
            context?.Dispose();
            context = null;
            throw new MemberUnavailableException(this, ExceptionMessages.UnavailableMember, e);
        }
        finally
        {
            if (lockTaken)
                accessLock.Release();

            Metrics?.ReportResponseTime(timeStamp.Elapsed);
            requestDurationTracker.Dispose();
        }
    }

    private protected sealed override Task<Result<bool>> VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        return RequestAsync(ExecuteAsync, token);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        async ValueTask<Result<bool>> ExecuteAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            await protocol.WriteVoteRequestAsync(in localMember.Id, term, lastLogIndex, lastLogTerm, token).ConfigureAwait(false);
            protocol.Reset();
            return await protocol.ReadResultAsync(token).ConfigureAwait(false);
        }
    }

    private protected sealed override Task<Result<PreVoteResult>> PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        return RequestAsync(ExecuteAsync, token);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        async ValueTask<Result<PreVoteResult>> ExecuteAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            await protocol.WritePreVoteRequestAsync(in localMember.Id, term, lastLogIndex, lastLogTerm, token).ConfigureAwait(false);
            protocol.Reset();
            return await protocol.ReadPreVoteResultAsync(token).ConfigureAwait(false);
        }
    }

    private protected sealed override Task<long?> SynchronizeAsync(long commitIndex, CancellationToken token)
    {
        return RequestAsync(ExecuteAsync, token);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        async ValueTask<long?> ExecuteAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            await protocol.WriteSynchronizeRequestAsync(commitIndex, token).ConfigureAwait(false);
            protocol.Reset();
            return await protocol.ReadNullableInt64Async(token).ConfigureAwait(false);
        }
    }

    private protected sealed override Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(CancellationToken token)
    {
        return RequestAsync(ExecuteAsync, token);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<IReadOnlyDictionary<string, string>> ExecuteAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            await protocol.WriteMetadataRequestAsync(token).ConfigureAwait(false);
            protocol.Reset();
            return await protocol.ReadMetadataResponseAsync(buffer, token).ConfigureAwait(false);
        }
    }

    private protected sealed override Task<bool> ResignAsync(CancellationToken token)
    {
        return RequestAsync(ExecuteAsync, token);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<bool> ExecuteAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            await protocol.WriteResignRequestAsync(token).ConfigureAwait(false);
            protocol.Reset();
            return await protocol.ReadBoolAsync(token).ConfigureAwait(false);
        }
    }

    private protected sealed override Task<Result<bool>> InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
    {
        return RequestAsync(ExecuteAsync, token);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        async ValueTask<Result<bool>> ExecuteAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            await protocol.WriteInstallSnapshotRequestAsync(localMember.Id, term, snapshotIndex, snapshot, buffer, token).ConfigureAwait(false);
            protocol.Reset();
            return await protocol.ReadResultAsync(token).ConfigureAwait(false);
        }
    }

    private protected sealed override Task<Result<bool>> AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
    {
        return RequestAsync(ExecuteAsync, token);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        async ValueTask<Result<bool>> ExecuteAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            await protocol.WriteAppendEntriesRequestAsync<TEntry, TList>(localMember.Id, term, entries, prevLogIndex, prevLogTerm, commitIndex, config, applyConfig, buffer, token).ConfigureAwait(false);
            protocol.Reset();
            return await protocol.ReadResultAsync(token).ConfigureAwait(false);
        }
    }

    public sealed override async ValueTask CancelPendingRequestsAsync()
    {
        accessLock.CancelSuspendedCallers(new(canceled: true));
        await accessLock.AcquireAsync().ConfigureAwait(false);
        try
        {
            await (context?.DisposeAsync() ?? ValueTask.CompletedTask).ConfigureAwait(false);
        }
        finally
        {
            context = null;
            accessLock.Release();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            context?.Dispose();
            context = null;
            accessLock.Dispose();
        }

        base.Dispose(disposing);
    }
}