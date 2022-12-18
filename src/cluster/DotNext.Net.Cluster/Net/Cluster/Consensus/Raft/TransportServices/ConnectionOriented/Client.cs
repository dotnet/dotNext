using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Threading;
using Timestamp = Diagnostics.Timestamp;

internal abstract partial class Client : RaftClusterMember
{
    private protected interface IConnectionContext : IDisposable, IAsyncDisposable
    {
        ProtocolStream Protocol { get; }

        Memory<byte> Buffer { get; }
    }

    // this interface helps to inline async request/response parsing pipeline to RequestAsync method
    private interface IClientExchange<TResponse>
    {
        ValueTask RequestAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token);

        ValueTask<TResponse> ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token);
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

    private async Task<TResponse> RequestAsync<TExchange, TResponse>(TExchange exchange, CancellationToken token)
        where TExchange : notnull, IClientExchange<TResponse>
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
            await exchange.RequestAsync(context.Protocol, context.Buffer, requestDurationTracker.Token).ConfigureAwait(false);
            context.Protocol.Reset();
            var result = await exchange.ResponseAsync(context.Protocol, context.Buffer, requestDurationTracker.Token).ConfigureAwait(false);
            Touch();
            return result;
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
            Status = ClusterMemberStatus.Unavailable;

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

    public sealed override async ValueTask CancelPendingRequestsAsync()
    {
        await accessLock.StealAsync().ConfigureAwait(false);
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