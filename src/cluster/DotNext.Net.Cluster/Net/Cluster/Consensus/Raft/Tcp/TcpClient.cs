using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp;

using Buffers;
using Threading;
using TransportServices;
using Intrinsics = Runtime.Intrinsics;

/*
    This implementation doesn't support multiplexing over single TCP
    connection so CorrelationId header is not needed
*/
internal sealed class TcpClient : TcpTransport, IClient
{
    private sealed class ClientNetworkStream : PacketStream
    {
        internal ClientNetworkStream(Socket socket, bool useSsl)
            : base(socket, true, useSsl)
        {
        }

        internal Task Authenticate(SslClientAuthenticationOptions options, CancellationToken token)
            => ssl is null ? Task.CompletedTask : ssl.AuthenticateAsClientAsync(options, token);

        internal async Task Exchange(IExchange exchange, Memory<byte> buffer, CancellationToken token)
        {
            PacketHeaders headers;
            int count;
            bool waitForInput;
            ReadOnlyMemory<byte> response;
            do
            {
                (headers, count, waitForInput) = await exchange.CreateOutboundMessageAsync(AdjustToPayload(buffer), token).ConfigureAwait(false);

                // transmit packet to the remote endpoint
                await WritePacket(headers, buffer, count, token).ConfigureAwait(false);
                if (!waitForInput)
                    break;

                // read response
                (headers, response) = await ReadPacket(buffer, token).ConfigureAwait(false);
            }
            while (await exchange.ProcessInboundMessageAsync(headers, response, token).ConfigureAwait(false));
        }

        internal Task ShutdownAsync() => ssl is null ? Task.CompletedTask : ssl.ShutdownAsync();
    }

    private readonly AsyncExclusiveLock accessLock;
    private volatile ClientNetworkStream? stream;

    internal TcpClient(IPEndPoint address, MemoryAllocator<byte> allocator, ILoggerFactory loggerFactory)
        : base(address, allocator, loggerFactory)
    {
        accessLock = new AsyncExclusiveLock();
        ConnectTimeout = TimeSpan.FromSeconds(30);
    }

    internal SslClientAuthenticationOptions? SslOptions { get; set; }

    internal TimeSpan ConnectTimeout { get; set; }

    private static void CancelConnectAsync(object? args)
        => Socket.CancelConnectAsync(Intrinsics.Cast<SocketAsyncEventArgs>(args));

    private static async Task<ClientNetworkStream> ConnectAsync(IPEndPoint endPoint, LingerOption linger, byte ttl, SslClientAuthenticationOptions? sslOptions, TimeSpan timeout, CancellationToken token)
    {
        using var timeoutControl = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutControl.CancelAfter(timeout);
        token = timeoutControl.Token;

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);

        try
        {
            await socket.ConnectAsync(endPoint, token).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        ConfigureSocket(socket, linger, ttl);
        ClientNetworkStream result;

        if (sslOptions is null)
        {
            result = new ClientNetworkStream(socket, useSsl: false);
        }
        else
        {
            result = new ClientNetworkStream(socket, useSsl: true);
            await result.Authenticate(sslOptions, token).ConfigureAwait(false);
        }

        return result;
    }

    private async ValueTask<ClientNetworkStream?> ConnectAsync(IExchange exchange, CancellationToken token)
    {
        ClientNetworkStream? result;
        AsyncLock.Holder lockHolder = default;
        try
        {
            lockHolder = await accessLock.AcquireLockAsync(token).ConfigureAwait(false);
            result = stream ??= await ConnectAsync(Address, LingerOption, Ttl, SslOptions, ConnectTimeout, token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            exchange.OnException(e);
            result = null;
        }
        finally
        {
            lockHolder.Dispose();
        }

        return result;
    }

    public async void Enqueue(IExchange exchange, CancellationToken token)
    {
        ThrowIfDisposed();
        var stream = this.stream;

        // establish connection if needed
        if (stream is null)
        {
            stream = await ConnectAsync(exchange, token).ConfigureAwait(false);
            if (stream is null)
                return;
        }

        AsyncLock.Holder lockHolder = default;

        // allocate single buffer for this exchange session
        MemoryOwner<byte> buffer = default;
        try
        {
            buffer = AllocTransmissionBlock();
            lockHolder = await accessLock.AcquireLockAsync(token).ConfigureAwait(false);
            await stream.Exchange(exchange, buffer.Memory, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException e)
        {
            Interlocked.Exchange(ref this.stream, null)?.Dispose();
            exchange.OnCanceled(e.CancellationToken);
        }
        catch (Exception e) when (e is SocketException || e.InnerException is SocketException || e is EndOfStreamException)
        {
            // broken socket detected
            Interlocked.Exchange(ref this.stream, null)?.Dispose();
            exchange.OnException(e);
        }
        catch (Exception e)
        {
            Interlocked.Exchange(ref this.stream, null)?.Dispose();
            exchange.OnException(e);
        }
        finally
        {
            buffer.Dispose();
            lockHolder.Dispose();
        }
    }

    private static async Task ShutdownConnectionGracefully(ClientNetworkStream stream, TimeSpan flushTimeout)
    {
        using (stream)
        {
            await stream.ShutdownAsync().ConfigureAwait(false);
            stream.Close((int)flushTimeout.TotalMilliseconds);
        }
    }

    public ValueTask CancelPendingRequestsAsync()
    {
        accessLock.CancelSuspendedCallers(new CancellationToken(true));
        var stream = Interlocked.Exchange(ref this.stream, null);
        return stream is null ? new() : new(ShutdownConnectionGracefully(stream, ConnectTimeout));
    }

    protected override void Dispose(bool disposing)
    {
        // set IsDisposed flag earlier to avoid ObjectDisposeException in Enqueue method
        // when it attempts to release the lock
        base.Dispose(disposing);

        if (disposing)
        {
            Interlocked.Exchange(ref stream, null)?.Dispose();
            accessLock.Dispose();
        }
    }
}