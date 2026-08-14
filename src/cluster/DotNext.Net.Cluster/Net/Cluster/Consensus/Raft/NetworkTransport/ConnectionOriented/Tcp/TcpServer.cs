using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft.NetworkTransport.ConnectionOriented.Tcp;

using Reflection;
using Threading;

internal sealed class TcpServer : Server, ITcpTransport
{
    private readonly Socket socket;
    private readonly int backlog, transmissionBlockSize;
    private readonly byte ttl;
    private readonly CancellationToken lifecycleToken;
    private readonly TimeSpan receiveTimeout;
    private readonly LingerOption linger;
    private readonly int gracefulShutdownTimeout;
    private readonly TaskCompletionSource noPendingConnectionsEvent;
    private readonly CancellationTokenMultiplexer multiplexer;

    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private volatile CancellationTokenSource? transmissionState;
    private volatile int connections;

    internal TcpServer(EndPoint address, int backlog, ILocalMember localMember, ILoggerFactory loggerFactory)
        : base(address, localMember, loggerFactory)
    {
        socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        this.backlog = backlog;
        transmissionState = new();
        lifecycleToken = transmissionState.Token; // cache token here to avoid ObjectDisposedException in HandleConnection
        linger = ITcpTransport.CreateDefaultLingerOption();
        gracefulShutdownTimeout = 1000;
        ttl = ITcpTransport.DefaultTtl;
        transmissionBlockSize = ITcpTransport.MinTransmissionBlockSize;
        noPendingConnectionsEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        multiplexer = new() { MaximumRetained = backlog };
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

    public int TransmissionBlockSize
    {
        get => transmissionBlockSize;
        init => transmissionBlockSize = ITcpTransport.ValidateTransmissionBlockSize(value);
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
        CancellationTokenMultiplexer.Scope timeoutSource;

        // TLS handshake
        if (SslOptions is null)
        {
            protocol = new(transport, MemoryAllocator, transmissionBlockSize);
            timeoutSource = default;
        }
        else
        {
            var ssl = new SslStream(transport, leaveInnerStreamOpen: true);
            timeoutSource = multiplexer.Combine(receiveTimeout, lifecycleToken);
            try
            {
                await ssl.AuthenticateAsServerAsync(SslOptions, timeoutSource.Token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                await ssl.DisposeAsync().ConfigureAwait(false);
                await transport.DisposeAsync().ConfigureAwait(false);
                logger.TlsHandshakeFailed(clientAddress, e);
                return;
            }
            finally
            {
                await timeoutSource.DisposeAsync().ConfigureAwait(false);
            }

            protocol = new(ssl, MemoryAllocator, transmissionBlockSize);
        }

        Interlocked.Increment(ref connections);
        try
        {
            // message processing loop
            for (; transport.Connected && !IsDisposingOrDisposed && !lifecycleToken.IsCancellationRequested; protocol.Reset())
            {
                var messageType = await protocol.ReadMessageTypeAsync(lifecycleToken).ConfigureAwait(false);
                if (messageType is MessageType.None)
                    break;

                timeoutSource = multiplexer.Combine(receiveTimeout, lifecycleToken);
                try
                {
                    await ProcessRequestAsync(messageType, protocol, timeoutSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException e) when (e.CausedByTimeout(timeoutSource))
                {
                    logger.RequestTimedOut(clientAddress, e);
                    break;
                }
                finally
                {
                    // reset cancellation token
                    await timeoutSource.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        catch (Exception e) when (e is SocketException { SocketErrorCode: SocketError.ConnectionReset } or { InnerException: SocketException { SocketErrorCode: SocketError.ConnectionReset } })
        {
            logger.ConnectionWasResetByClient(clientAddress);
        }
        catch (OperationCanceledException)
        {
            // shutdown socket gracefully without logging
        }
        catch (Exception e)
        {
            logger.FailedToProcessRequest(clientAddress, e);
        }
        finally
        {
            await protocol.DisposeAsync().ConfigureAwait(false);
            if (protocol.BaseStream is SslStream ssl)
                await ssl.DisposeAsync().ConfigureAwait(false);
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
            catch (Exception e) when (e is ObjectDisposedException ||
                                      (e is OperationCanceledException canceledEx && canceledEx.CancellationToken == lifecycleToken))
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

        if (connections is 0)
            noPendingConnectionsEvent.TrySetResult();
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

    private void CleanUp()
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
            CleanUp();
            if (!SpinWait.SpinUntil(noPendingConnectionsEvent.Task.IsCompletedGetter, GracefulShutdownTimeout))
                logger.TcpGracefulShutdownFailed(GracefulShutdownTimeout);
        }

        base.Dispose(disposing);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        CleanUp();
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
