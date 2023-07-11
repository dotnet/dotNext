using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
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
    private volatile Task? listenerTask;

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

    private protected override MemoryOwner<byte> AllocateBuffer(int bufferSize)
        => defaultAllocator(bufferSize);

    private async void HandleConnection(ConnectionContext connection, int transmissionSize, EndPoint? clientAddress, MemoryAllocator<byte> allocator)
    {
        var protocol = new ProtocolPipeStream(connection.Transport, allocator, transmissionSize)
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

    private async Task Listen(IConnectionListener listener)
    {
        try
        {
            while (!lifecycleToken.IsCancellationRequested && !IsDisposingOrDisposed)
            {
                try
                {
                    var connection = await listener.AcceptAsync(lifecycleToken).ConfigureAwait(false);
                    if (connection is null)
                        break;

                    ThreadPool.UnsafeQueueUserWorkItem(new ConnectionHandler(this, connection), preferLocal: false);
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
        finally
        {
            await listener.DisposeAsync().ConfigureAwait(false);
        }
    }

    public override async ValueTask StartAsync(CancellationToken token)
        => listenerTask = Listen(await factory.BindAsync(Address, token).ConfigureAwait(false));

    private void Cleanup()
    {
        using var tokenSource = Interlocked.Exchange(ref transmissionState, null);
        tokenSource?.Cancel(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Cleanup();
        }

        base.Dispose(disposing);
    }

    protected override ValueTask DisposeAsyncCore()
    {
        Cleanup();
        return new(listenerTask ?? Task.CompletedTask);
    }

    private sealed class ConnectionHandler : Tuple<ConnectionContext, int, EndPoint?, MemoryAllocator<byte>>, IThreadPoolWorkItem
    {
        private readonly WeakReference<GenericServer> server;

        internal ConnectionHandler(GenericServer server, ConnectionContext connection)
            : base(connection, GetTransmissionSize(connection), connection.RemoteEndPoint, GetMemoryAllocator(server, connection))
            => this.server = new(server, trackResurrection: false);

        private static int GetTransmissionSize(ConnectionContext connection)
            => connection.Transport.Output.GetSpan().Length;

        private static MemoryAllocator<byte> GetMemoryAllocator(GenericServer server, BaseConnectionContext connection)
        {
            return connection.Features.Get<MemoryAllocator<byte>>()
                ?? connection.Features.Get<IMemoryPoolFeature>()?.MemoryPool?.ToAllocator()
                ?? server.defaultAllocator;
        }

        void IThreadPoolWorkItem.Execute()
        {
            if (this.server.TryGetTarget(out var server))
                server.HandleConnection(Item1, Item2, Item3, Item4);
        }
    }
}