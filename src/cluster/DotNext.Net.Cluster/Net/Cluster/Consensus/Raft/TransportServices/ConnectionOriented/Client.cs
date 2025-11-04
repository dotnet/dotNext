using System.Net;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Threading;
using ConcurrentTypeMap = Collections.Specialized.ConcurrentTypeMap;
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
        ValueTask RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token);

        static abstract ValueTask<TResponse> ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token);

        static abstract string Name { get; }
    }

    private readonly AsyncExclusiveLock accessLock;
    private readonly TimeSpan connectTimeout;
    private readonly ConcurrentTypeMap exchangeCache;

    // connection context and its devirtualized members
    private IConnectionContext? context;
    private Memory<byte> bufferCache;
    private ProtocolStream? protocolCache;

    private protected Client(ILocalMember localMember, EndPoint endPoint)
        : base(localMember, endPoint)
    {
        accessLock = new()
        {
            MeasurementTags = new()
            {
                { IRaftClusterMember.RemoteAddressMeterAttributeName, endPoint.ToString() },
            },
        };

        connectTimeout = TimeSpan.FromSeconds(1);
        exchangeCache = new();
    }

    internal TimeSpan ConnectTimeout
    {
        get => connectTimeout;
        init => connectTimeout = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    private protected abstract ValueTask<IConnectionContext> ConnectAsync(CancellationToken token);

    private async Task<TResponse> RequestAsync<TResponse, TExchange>(TExchange exchange, CancellationToken token)
        where TExchange : IClientExchange<TResponse>
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var timeStamp = new Timestamp();
        var lockTaken = false;

        var requestDurationTracker = CancellationTokenSource.CreateLinkedTokenSource(token);
        try
        {
            requestDurationTracker.CancelAfter(RequestTimeout);
            await accessLock.AcquireAsync(requestDurationTracker.Token).ConfigureAwait(false);
            lockTaken = true;

            if (context is null)
            {
                context = await ConnectAsync(requestDurationTracker.Token).ConfigureAwait(false);
                bufferCache = context.Buffer;
                protocolCache = context.Protocol;
            }
            else
            {
                Debug.Assert(protocolCache is not null);
            }

            protocolCache.Reset();
            await exchange.RequestAsync(localMember, protocolCache, bufferCache, requestDurationTracker.Token).ConfigureAwait(false);
            protocolCache.Reset();
            var result = await TExchange.ResponseAsync(protocolCache, bufferCache, requestDurationTracker.Token).ConfigureAwait(false);
            Touch();
            return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // canceled by caller
            ClearContext();
            throw;
        }
        catch (Exception e)
        {
            Logger.MemberUnavailable(EndPoint, e);
            Status = ClusterMemberStatus.Unavailable;

            // detect broken socket
            ClearContext();
            throw new MemberUnavailableException(this, innerException: e);
        }
        finally
        {
            if (lockTaken)
                accessLock.Release();

            ResponseTimeMeter.Record(
                timeStamp.ElapsedMilliseconds,
                new(IRaftClusterMember.MessageTypeAttributeName, TExchange.Name),
                cachedRemoteAddressAttribute);

            requestDurationTracker.Dispose();

            if (exchange is IResettable)
            {
                ((IResettable)exchange).Reset();
                exchangeCache.TryAdd(exchange);
            }
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
            ClearContext();
            accessLock.Release();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearContext()
    {
        if (context is not null)
        {
            context.Dispose();
            context = null;
            protocolCache = null;
            bufferCache = default;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClearContext();
            accessLock.Dispose();
        }

        base.Dispose(disposing);
    }
}