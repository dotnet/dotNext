using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp;

using Buffers;
using Diagnostics;
using Threading;
using TransportServices;
using TransportServices.ConnectionOriented;
using IClusterConfiguration = Membership.IClusterConfiguration;

internal sealed class TcpClient : RaftClusterMember, ITcpTransport
{
    private readonly AsyncExclusiveLock accessLock;
    private readonly IPEndPoint address;
    private readonly MemoryAllocator<byte> allocator;
    private readonly int transmissionBlockSize;
    private readonly byte ttl;
    private readonly LingerOption linger;
    private readonly TimeSpan connectTimeout;
    private TcpStream? transport;
    private ProtocolStream? protocol;

    internal TcpClient(ILocalMember localMember, IPEndPoint endPoint, ClusterMemberId id, MemoryAllocator<byte> allocator)
        : base(localMember, endPoint, id)
    {
        accessLock = new();
        this.allocator = allocator;
        transmissionBlockSize = ITcpTransport.MinTransmissionBlockSize;
        ttl = ITcpTransport.DefaultTtl;
        linger = ITcpTransport.CreateDefaultLingerOption();
        address = endPoint;
        connectTimeout = TimeSpan.FromSeconds(1);
    }

    internal TimeSpan ConnectTimeout
    {
        get => connectTimeout;
        init => connectTimeout = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public SslClientAuthenticationOptions? SslOptions
    {
        get;
        init;
    }

    public int TransmissionBlockSize
    {
        get => transmissionBlockSize;
        init => transmissionBlockSize = ITcpTransport.ValidateTranmissionBlockSize(value);
    }

    public byte Ttl
    {
        get => ttl;
        init => ttl = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public LingerOption LingerOption
    {
        get => linger;
        init => linger = value ?? throw new ArgumentNullException(nameof(value));
    }

    IPEndPoint INetworkTransport.Address => address;

    private async Task ConnectAsync(CancellationToken token)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(address, token).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        ITcpTransport.ConfigureSocket(socket, linger, ttl);
        transport = new(socket, owns: true);
        transport.WriteTimeout = (int)RequestTimeout.TotalMilliseconds;
        if (SslOptions is null)
        {
            protocol = new(transport, allocator, transmissionBlockSize);
        }
        else
        {
            var ssl = new SslStream(transport, leaveInnerStreamOpen: true);
            try
            {
                await ssl.AuthenticateAsClientAsync(SslOptions, token).ConfigureAwait(false);
            }
            catch
            {
                await transport.DisposeAsync().ConfigureAwait(false);
                await ssl.DisposeAsync().ConfigureAwait(false);
                transport = null;
                throw;
            }

            protocol = new(ssl, allocator, transmissionBlockSize);
        }
    }

    public override async ValueTask CancelPendingRequestsAsync()
    {
        accessLock.CancelSuspendedCallers(new(canceled: true));
        await accessLock.AcquireAsync().ConfigureAwait(false);
        try
        {
            if (protocol?.BaseStream is SslStream ssl)
            {
                using (ssl)
                    await ssl.ShutdownAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            protocol?.Dispose();
            protocol = null;

            transport?.Close((int)RequestTimeout.TotalMilliseconds);
            transport = null;

            accessLock.Release();
        }
    }

    private async Task<TResponse> RequestAsync<TResponse>(Func<ProtocolStream, CancellationToken, ValueTask<TResponse>> request, CancellationToken token)
    {
        ThrowIfDisposed();

        var timeStamp = new Timestamp();
        var lockTaken = false;

        var requestDurationTracker = CancellationTokenSource.CreateLinkedTokenSource(token);
        requestDurationTracker.CancelAfter(RequestTimeout);
        try
        {
            await accessLock.AcquireAsync(requestDurationTracker.Token).ConfigureAwait(false);
            lockTaken = true;

            if (protocol is null)
            {
                // connection has separated timeout
                using var connectDurationTracker = CancellationTokenSource.CreateLinkedTokenSource(requestDurationTracker.Token);
                connectDurationTracker.CancelAfter(ConnectTimeout);
                await ConnectAsync(connectDurationTracker.Token).ConfigureAwait(false);
            }

            Debug.Assert(protocol is not null);
            protocol.Reset();
            return await request(protocol, requestDurationTracker.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == token)
        {
            DestroyConnection();
            throw;
        }
        catch (Exception e)
        {
            Logger.MemberUnavailable(address, e);
            ChangeStatus(ClusterMemberStatus.Unavailable);

            // detect broken socket
            DestroyConnection();
            throw new MemberUnavailableException(this, ExceptionMessages.UnavailableMember, e);
        }
        finally
        {
            if (lockTaken)
                accessLock.Release();

            Metrics?.ReportResponseTime(timeStamp.Elapsed);
            requestDurationTracker.Dispose();
        }

        void DestroyConnection()
        {
            protocol?.Dispose();
            protocol = null;

            transport?.Dispose();
            transport = null;
        }
    }

    private protected override Task<Result<bool>> VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        return RequestAsync(ExecuteAsync, token);

        [AsyncStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        async ValueTask<Result<bool>> ExecuteAsync(ProtocolStream protocol, CancellationToken token)
        {
            await protocol.WriteVoteRequestAsync(in localMember.Id, term, lastLogIndex, lastLogTerm, token).ConfigureAwait(false);
            protocol.Reset();
            return await protocol.ReadResultAsync(token).ConfigureAwait(false);
        }
    }

    private protected override Task<Result<PreVoteResult>> PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        return RequestAsync(ExecuteAsync, token);

        [AsyncStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        async ValueTask<Result<PreVoteResult>> ExecuteAsync(ProtocolStream protocol, CancellationToken token)
        {
            await protocol.WritePreVoteRequestAsync(in localMember.Id, term, lastLogIndex, lastLogTerm, token).ConfigureAwait(false);
            protocol.Reset();
            return await protocol.ReadPreVoteResultAsync(token).ConfigureAwait(false);
        }
    }

    private protected override Task<long?> SynchronizeAsync(CancellationToken token)
    {
        return RequestAsync(ExecuteAsync, token);

        [AsyncStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<long?> ExecuteAsync(ProtocolStream protocol, CancellationToken token)
        {
            await protocol.WriteSynchronizeRequestAsync(token).ConfigureAwait(false);
            protocol.Reset();
            return await protocol.ReadNullableInt64Async(token).ConfigureAwait(false);
        }
    }

    private protected override Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(CancellationToken token)
    {
        return RequestAsync(ExecuteAsync, token);

        [AsyncStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        async ValueTask<IReadOnlyDictionary<string, string>> ExecuteAsync(ProtocolStream protocol, CancellationToken token)
        {
            await protocol.WriteMetadataRequestAsync(token).ConfigureAwait(false);
            protocol.Reset();
            using var buffer = allocator.Invoke(transmissionBlockSize, exactSize: false);
            return await protocol.ReadMetadataResponseAsync(buffer.Memory, token).ConfigureAwait(false);
        }
    }

    private protected override Task<bool> ResignAsync(CancellationToken token)
    {
        return RequestAsync(ExecuteAsync, token);

        [AsyncStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<bool> ExecuteAsync(ProtocolStream protocol, CancellationToken token)
        {
            await protocol.WriteResignRequestAsync(token).ConfigureAwait(false);
            protocol.Reset();
            return await protocol.ReadBoolAsync(token).ConfigureAwait(false);
        }
    }

    private protected override Task<Result<bool>> InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
    {
        return RequestAsync(ExecuteAsync, token);

        [AsyncStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        async ValueTask<Result<bool>> ExecuteAsync(ProtocolStream protocol, CancellationToken token)
        {
            using var buffer = allocator.Invoke(transmissionBlockSize, exactSize: false);
            await protocol.WriteInstallSnapshotRequestAsync(localMember.Id, term, snapshotIndex, snapshot, buffer.Memory, token).ConfigureAwait(false);
            protocol.Reset();
            return await protocol.ReadResultAsync(token).ConfigureAwait(false);
        }
    }

    private protected override Task<Result<bool>> AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
    {
        return RequestAsync(ExecuteAsync, token);

        [AsyncStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        async ValueTask<Result<bool>> ExecuteAsync(ProtocolStream protocol, CancellationToken token)
        {
            using var buffer = allocator.Invoke(transmissionBlockSize, exactSize: false);
            await protocol.WriteAppendEntriesRequestAsync<TEntry, TList>(localMember.Id, term, entries, prevLogIndex, prevLogTerm, commitIndex, config, applyConfig, buffer.Memory, token).ConfigureAwait(false);
            protocol.Reset();
            return await protocol.ReadResultAsync(token).ConfigureAwait(false);
        }
    }

    protected override void Dispose(bool disposing)
    {
        // set IsDisposed flag earlier to avoid ObjectDisposeException in Enqueue method
        // when it attempts to release the lock
        base.Dispose(disposing);

        if (disposing)
        {
            protocol?.Dispose();
            transport?.Dispose();
            accessLock.Dispose();
        }
    }
}