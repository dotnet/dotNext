using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp;

using Buffers;
using TransportServices;
using TransportServices.ConnectionOriented;
using static Reflection.TaskType;

internal sealed class TcpServer : Server, ITcpTransport
{
    private readonly Socket socket;
    private readonly int backlog, transmissionBlockSize;
    private readonly byte ttl;
    private readonly CancellationToken lifecycleToken;
    private readonly TimeSpan receiveTimeout;
    private readonly LingerOption linger;
    private readonly MemoryAllocator<byte> allocator;
    private readonly int gracefulShutdownTimeout;
    private readonly TaskCompletionSource noPendingConnectionsEvent;

    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private volatile CancellationTokenSource? transmissionState;
    private volatile int connections;

    internal TcpServer(EndPoint address, int backlog, ILocalMember localMember, MemoryAllocator<byte> allocator, ILoggerFactory loggerFactory)
        : base(address, localMember, loggerFactory)
    {
        socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        this.backlog = backlog;
        transmissionState = new();
        lifecycleToken = transmissionState.Token; // cache token here to avoid ObjectDisposedException in HandleConnection
        linger = ITcpTransport.CreateDefaultLingerOption();
        this.allocator = allocator;
        gracefulShutdownTimeout = 1000;
        ttl = ITcpTransport.DefaultTtl;
        transmissionBlockSize = ITcpTransport.MinTransmissionBlockSize;
        noPendingConnectionsEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public override TimeSpan ReceiveTimeout
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

    private protected override MemoryOwner<byte> AllocateBuffer(int bufferSize) => allocator(bufferSize);

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
        var clientAddress = remoteClient.RemoteEndPoint;
        var transport = new TcpStream(remoteClient, owns: true);
        TcpProtocolStream protocol;
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
                logger.TlsHandshakeFailed(clientAddress, e);
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
            while (transport.Connected && !IsDisposingOrDisposed && !lifecycleToken.IsCancellationRequested)
            {
                var messageType = await protocol.ReadMessageTypeAsync(lifecycleToken).ConfigureAwait(false);
                if (messageType is MessageType.None)
                    break;

                timeoutSource.CancelAfter(receiveTimeout);
                await ProcessRequestAsync(messageType, protocol, timeoutSource.Token).ConfigureAwait(false);
                protocol.Reset();

                // reset cancellation token
                timeoutSource.Dispose();
                timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(lifecycleToken);
            }
        }
        catch (Exception e) when (e is SocketException { SocketErrorCode: SocketError.ConnectionReset } || e.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset })
        {
            logger.ConnectionWasResetByClient(clientAddress);
        }
        catch (OperationCanceledException e)
        {
            // if lifecycleToken is canceled then shutdown socket gracefully without logging
            if (!lifecycleToken.IsCancellationRequested)
                logger.RequestTimedOut(clientAddress, e);
        }
        catch (Exception e)
        {
            logger.FailedToProcessRequest(clientAddress, e);
        }
        finally
        {
            protocol.Dispose();
            (protocol.BaseStream as SslStream)?.Dispose();
            timeoutSource.Dispose();
            transport.Close(GracefulShutdownTimeout);
            if (Interlocked.Decrement(ref connections) <= 0 && IsDisposingOrDisposed)
                noPendingConnectionsEvent.TrySetResult();
        }
    }

    private async void Listen()
    {
        while (!lifecycleToken.IsCancellationRequested && !IsDisposingOrDisposed)
        {
            try
            {
                var remoteClient = await socket.AcceptAsync(lifecycleToken).ConfigureAwait(false);
                ITcpTransport.ConfigureSocket(remoteClient, linger, ttl);
                ThreadPool.UnsafeQueueUserWorkItem(HandleConnection, remoteClient, preferLocal: false);
            }
            catch (Exception e) when (e is ObjectDisposedException || (e is OperationCanceledException canceledEx && canceledEx.CancellationToken == lifecycleToken))
            {
                break;
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

                break;
            }
            catch (Exception e)
            {
                logger.SocketAcceptLoopTerminated(e);
                break;
            }
        }
    }

    public override ValueTask StartAsync(CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = ValueTask.CompletedTask;
            try
            {
                socket.Bind(Address);
                socket.Listen(backlog);
                Listen();
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    private void Cleanup()
    {
        var tokenSource = Interlocked.Exchange(ref transmissionState, null);
        try
        {
            tokenSource?.Cancel(false);
        }
        finally
        {
            socket.Dispose();
            tokenSource?.Dispose();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Cleanup();
            if (!SpinWait.SpinUntil(noPendingConnectionsEvent.Task.GetIsCompletedGetter(), GracefulShutdownTimeout))
                logger.TcpGracefulShutdownFailed(GracefulShutdownTimeout);
        }

        base.Dispose(disposing);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        Cleanup();
        try
        {
            await noPendingConnectionsEvent.Task.WaitAsync(TimeSpan.FromMilliseconds(GracefulShutdownTimeout)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            logger.TcpGracefulShutdownFailed(GracefulShutdownTimeout);
        }
    }
}