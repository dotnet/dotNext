using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.CustomTransport;

using Buffers;
using TransportServices;
using TransportServices.ConnectionOriented;
using static Threading.LinkedTokenSourceFactory;

internal sealed class GenericServer : Server
{
    private readonly IConnectionListenerFactory factory;
    private readonly CancellationToken lifecycleToken;
    private readonly MemoryAllocator<byte> defaultAllocator;

    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private volatile CancellationTokenSource? transmissionState;

    internal GenericServer(EndPoint address, IConnectionListenerFactory listenerFactory, ILocalMember localMember, MemoryAllocator<byte> defaultAllocator, ILoggerFactory loggerFactory)
        : base(address, localMember, loggerFactory)
    {
        Debug.Assert(listenerFactory is not null);
        Debug.Assert(defaultAllocator is not null);

        transmissionState = new();
        lifecycleToken = transmissionState.Token; // cache token here to avoid ObjectDisposedException in HandleConnection
        factory = listenerFactory;
        this.defaultAllocator = defaultAllocator;
    }

    public override TimeSpan ReceiveTimeout
    {
        get;
        init;
    }

    private async void HandleConnection(ConnectionContext connection)
    {
        var clientAddress = connection.RemoteEndPoint;

        // determine transmission size
        var transmissionSize = connection.Transport.Output.GetSpan().Length;
        var protocol = new ProtocolPipeStream(connection.Transport, connection.Features.Get<MemoryAllocator<byte>>() ?? defaultAllocator, transmissionSize)
        {
            WriteTimeout = (int)ReceiveTimeout.TotalMilliseconds,
            ReadTimeout = (int)ReceiveTimeout.TotalMilliseconds,
        };

        var token = lifecycleToken;
        var tokenSource = token.LinkTo(connection.ConnectionClosed);
        try
        {
            while (!IsDisposingOrDisposed && !token.IsCancellationRequested)
            {
                var messageType = await protocol.ReadMessageTypeAsync(token).ConfigureAwait(false);
                if (messageType is MessageType.None)
                    break;

                tokenSource?.CancelAfter(ReceiveTimeout);
                await ProcessRequestAsync(messageType, protocol, token).ConfigureAwait(false);
                protocol.Reset();

                // reset cancellation token
                tokenSource?.Dispose();
                token = lifecycleToken;
                tokenSource = token.LinkTo(connection.ConnectionClosed);
            }
        }
        catch (ConnectionResetException)
        {
            // reset by client
            logger.ConnectionWasResetByClient(clientAddress);
        }
        catch (OperationCanceledException) when (tokenSource is not null && tokenSource.CancellationOrigin == connection.ConnectionClosed)
        {
            // closed by client
            logger.ConnectionWasResetByClient(clientAddress);
        }
        catch (OperationCanceledException) when (tokenSource is not null && tokenSource.CancellationOrigin == lifecycleToken)
        {
            // server stopped, suppress exception
        }
        catch (OperationCanceledException e)
        {
            // timeout
            logger.RequestTimedOut(clientAddress, e);
        }
        catch (Exception e)
        {
            logger.FailedToProcessRequest(clientAddress, e);
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            protocol.Dispose();
            tokenSource?.Dispose();
        }
    }

    private async void Listen(IConnectionListener listener)
    {
        await using (listener.ConfigureAwait(false))
        {
            while (!lifecycleToken.IsCancellationRequested && !IsDisposingOrDisposed)
            {
                try
                {
                    var connection = await listener.AcceptAsync(lifecycleToken).ConfigureAwait(false);
                    if (connection is null)
                        break;

                    ThreadPool.UnsafeQueueUserWorkItem(HandleConnection, connection, preferLocal: false);
                }
                catch (Exception e) when (e is ObjectDisposedException || (e is OperationCanceledException canceledEx && canceledEx.CancellationToken == lifecycleToken))
                {
                    break;
                }
                catch (Exception e)
                {
                    logger.SocketAcceptLoopTerminated(e);
                    break;
                }
            }

            await listener.UnbindAsync(lifecycleToken).ConfigureAwait(false);
        }
    }

    public override async ValueTask StartAsync(CancellationToken token)
        => Listen(await factory.BindAsync(Address, token).ConfigureAwait(false));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var tokenSource = Interlocked.Exchange(ref transmissionState, null);
            try
            {
                tokenSource?.Cancel(false);
            }
            finally
            {
                tokenSource?.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}