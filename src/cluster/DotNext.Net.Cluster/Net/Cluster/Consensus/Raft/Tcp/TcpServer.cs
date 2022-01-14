using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp;

using Buffers;
using IO.Log;
using TransportServices;
using TransportServices.ConnectionOriented;

internal sealed class TcpServer : Disposable, IServer, ITcpTransport
{
    private readonly Socket socket;
    private readonly IPEndPoint address;
    private readonly int backlog, transmissionBlockSize;
    private readonly byte ttl;
    private readonly CancellationTokenSource transmissionState;
    private readonly CancellationToken lifecycleToken;
    private readonly TimeSpan receiveTimeout;
    private readonly ILogger logger;
    private readonly LingerOption linger;
    private readonly MemoryAllocator<byte> allocator;
    private readonly ILocalMember localMember;
    private readonly int gracefulShutdownTimeout;
    private volatile int connections;

    internal TcpServer(IPEndPoint address, int backlog, ILocalMember localMember, MemoryAllocator<byte> allocator, ILoggerFactory loggerFactory)
    {
        socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        this.backlog = backlog;
        transmissionState = new();
        lifecycleToken = transmissionState.Token; // cache token here to avoid ObjectDisposedException in HandleConnection
        linger = ITcpTransport.CreateDefaultLingerOption();
        this.allocator = allocator;
        this.address = address;
        gracefulShutdownTimeout = 1000;
        this.localMember = localMember;
        logger = loggerFactory.CreateLogger(GetType());
    }

    public TimeSpan ReceiveTimeout
    {
        get => receiveTimeout;
        init
        {
            socket.ReceiveTimeout = (int)value.TotalMilliseconds;
            receiveTimeout = value;
        }
    }

    public SslServerAuthenticationOptions? SslOptions
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

    public int GracefulShutdownTimeout
    {
        get => gracefulShutdownTimeout;
        init => gracefulShutdownTimeout = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    private async void HandleConnection(Socket remoteClient)
    {
        var transport = new TcpStream(remoteClient, owns: true);
        ProtocolStream protocol;
        CancellationTokenSource timeoutSource;

        // TLS handshake
        if (SslOptions is null)
        {
            protocol = new(transport, allocator, transmissionBlockSize);
        }
        else
        {
            var ssl = new SslStream(transport, leaveInnerStreamOpen: true);
            timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(lifecycleToken);
            timeoutSource.CancelAfter(receiveTimeout);
            try
            {
                await ssl.AuthenticateAsServerAsync(SslOptions, timeoutSource.Token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                ssl.Dispose();
                transport.Dispose();
                logger.TlsHandshakeFailed(remoteClient.RemoteEndPoint, e);
                return;
            }
            finally
            {
                timeoutSource.Dispose();
            }

            protocol = new(ssl, allocator, transmissionBlockSize);
        }

        Interlocked.Increment(ref connections);
        timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(lifecycleToken);
        try
        {
            // message processing loop
            while (transport.Connected && !IsDisposed && !lifecycleToken.IsCancellationRequested)
            {
                var messageType = await protocol.ReadMessageTypeAsync(lifecycleToken).ConfigureAwait(false);
                timeoutSource.CancelAfter(receiveTimeout);

                await ProcessRequestAsync(messageType, protocol, timeoutSource.Token).ConfigureAwait(false);
                protocol.Reset();

                timeoutSource.Dispose();
                timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(lifecycleToken);
            }
        }
        catch (Exception e) when (e is SocketException { SocketErrorCode: SocketError.ConnectionReset } || e.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset })
        {
            logger.ConnectionWasResetByClient(socket.RemoteEndPoint);
        }
        catch (OperationCanceledException e) when (e.CancellationToken != lifecycleToken)
        {
            logger.RequestTimedOut(remoteClient.RemoteEndPoint, e);
        }
        catch (Exception e)
        {
            logger.FailedToProcessRequest(socket.RemoteEndPoint, e);
        }
        finally
        {
            protocol.Dispose();
            timeoutSource.Dispose();
            transport.Close(GracefulShutdownTimeout);
            Interlocked.Decrement(ref connections);
        }
    }

    private ValueTask ProcessRequestAsync(MessageType type, ProtocolStream protocol, CancellationToken token) => type switch
    {
        MessageType.Vote => VoteAsync(protocol, token),
        MessageType.PreVote => PreVoteAsync(protocol, token),
        MessageType.Synchronize => SynchronizeAsync(protocol, token),
        MessageType.Metadata => GetMetadataAsync(protocol, token),
        MessageType.Resign => ResignAsync(protocol, token),
        MessageType.InstallSnapshot => InstallSnapshotAsync(protocol, token),
        MessageType.AppendEntries => AppendEntriesAsync(protocol, token),
        _ => ValueTask.FromException(new InvalidOperationException(ExceptionMessages.UnknownRaftMessageType(type))),
    };

    [AsyncIteratorStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask VoteAsync(ProtocolStream protocol, CancellationToken token)
    {
        var request = await protocol.ReadVoteRequestAsync(token).ConfigureAwait(false);
        var response = await localMember.VoteAsync(request.Id, request.Term, request.LastLogIndex, request.LastLogTerm, token).ConfigureAwait(false);
        protocol.Reset();
        await protocol.WriteResponseAsync(in response, token).ConfigureAwait(false);
    }

    [AsyncIteratorStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask PreVoteAsync(ProtocolStream protocol, CancellationToken token)
    {
        var request = await protocol.ReadPreVoteRequestAsync(token).ConfigureAwait(false);
        var response = await localMember.PreVoteAsync(request.Id, request.Term, request.LastLogIndex, request.LastLogTerm, token).ConfigureAwait(false);
        protocol.Reset();
        await protocol.WriteResponseAsync(in response, token).ConfigureAwait(false);
    }

    [AsyncIteratorStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask SynchronizeAsync(ProtocolStream protocol, CancellationToken token)
    {
        protocol.Reset();
        var commitIndex = await localMember.SynchronizeAsync(token).ConfigureAwait(false);
        await protocol.WriteResponseAsync(in commitIndex, token).ConfigureAwait(false);
    }

    [AsyncIteratorStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask GetMetadataAsync(ProtocolStream protocol, CancellationToken token)
    {
        protocol.Reset();
        using var buffer = allocator.Invoke(transmissionBlockSize, exactSize: false);
        await protocol.WriteMetadataResponseAsync(localMember.Metadata, buffer.Memory, token).ConfigureAwait(false);
    }

    [AsyncIteratorStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask ResignAsync(ProtocolStream protocol, CancellationToken token)
    {
        protocol.Reset();
        var response = await localMember.ResignAsync(token).ConfigureAwait(false);
        await protocol.WriteResponseAsync(response, token).ConfigureAwait(false);
    }

    [AsyncIteratorStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask InstallSnapshotAsync(ProtocolStream protocol, CancellationToken token)
    {
        var request = await protocol.ReadInstallSnapshotRequestAsync(token).ConfigureAwait(false);
        var response = await localMember.InstallSnapshotAsync(request.Id, request.Term, protocol.CreateSnapshot(in request.SnapshotMetadata), request.SnapshotIndex, token).ConfigureAwait(false);
        if (!response.Value)
        {
            // skip contents of snapshot
            await protocol.SkipAsync(token).ConfigureAwait(false);
        }

        protocol.Reset();
        await protocol.WriteResponseAsync(in response, token).ConfigureAwait(false);
    }

    [AsyncIteratorStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask AppendEntriesAsync(ProtocolStream protocol, CancellationToken token)
    {
        var request = await protocol.ReadAppendEntriesRequestAsync(allocator, token).ConfigureAwait(false);
        Result<bool> response;
        await using (request.Entries.ConfigureAwait(false))
        {
            using (request.Configuration)
                response = await localMember.AppendEntriesAsync(request.Id, request.Term, request.Entries, request.PrevLogIndex, request.PrevLogTerm, request.CommitIndex, request.Configuration, request.ApplyConfig, token).ConfigureAwait(false);

            // skip remaining log entries
            while (await request.Entries.MoveNextAsync().ConfigureAwait(false));
        }

        protocol.Reset();
        await protocol.WriteResponseAsync(in response, token).ConfigureAwait(false);
    }

    private async void Listen()
    {
        for (var pending = true; pending && !IsDisposed;)
        {
            try
            {
                var remoteClient = await socket.AcceptAsync(lifecycleToken).ConfigureAwait(false);
                ITcpTransport.ConfigureSocket(remoteClient, linger, ttl);
                ThreadPool.QueueUserWorkItem(HandleConnection, remoteClient, preferLocal: false);
            }
            catch (ObjectDisposedException)
            {
                pending = false;
            }
            catch (SocketException e)
            {
                switch (e.SocketErrorCode)
                {
                    case SocketError.OperationAborted:
                    case SocketError.ConnectionAborted:
                    case SocketError.Shutdown:
                        break;
                    default:
                        logger.SockerErrorOccurred(e.SocketErrorCode);
                        break;
                }

                pending = false;
            }
            catch (Exception e)
            {
                logger.SocketAcceptLoopTerminated(e);
                pending = false;
            }
        }
    }

    public void Start()
    {
        socket.Bind(address);
        socket.Listen(backlog);
        Listen();
    }

    private bool NoMoreConnections() => connections <= 0;

    IPEndPoint INetworkTransport.Address => address;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try
            {
                if (!transmissionState.IsCancellationRequested)
                    transmissionState.Cancel(false);
            }
            finally
            {
                transmissionState.Dispose();
                socket.Dispose();
            }

            if (!SpinWait.SpinUntil(NoMoreConnections, GracefulShutdownTimeout))
                logger.TcpGracefulShutdownFailed(GracefulShutdownTimeout);
        }
    }
}