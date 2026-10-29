using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft.NetworkTransport.ConnectionOriented.Custom;

using Buffers;
using Runtime.CompilerServices;
using Threading;

internal sealed class GenericServer : Server
{
    private readonly CancellationToken lifecycleToken;
    private readonly CancellationTokenMultiplexer multiplexer;

    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private volatile CancellationTokenSource? transmissionState;
    private volatile Task? listenerTask;

    internal GenericServer(EndPoint address, ILocalMember localMember, ILoggerFactory loggerFactory)
        : base(address, localMember, loggerFactory)
    {
        transmissionState = new();
        lifecycleToken = transmissionState.Token; // cache token here to avoid ObjectDisposedException in HandleConnection

        multiplexer = new() { MaximumRetained = 100 };
    }
    
    public required IConnectionListenerFactory ListenerFactory { get; init; }

    public override TimeSpan ReceiveTimeout
    {
        get;
        init;
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task HandleConnection(ConnectionContext connection, int transmissionSize, EndPoint? clientAddress, MemoryAllocator<byte> allocator)
    {
        var protocol = new ProtocolPipeStream(connection.Transport, allocator, transmissionSize)
        {
            WriteTimeout = (int)ReceiveTimeout.TotalMilliseconds,
            ReadTimeout = (int)ReceiveTimeout.TotalMilliseconds,
        };

        var cts = default(CancellationTokenMultiplexer.ScopeWithTimeout);
        try
        {
            while (!IsDisposingOrDisposed && !lifecycleToken.IsCancellationRequested)
            {
                cts = multiplexer.CombineAndSetTimeoutLater(lifecycleToken, connection.ConnectionClosed);
                var messageType = await protocol.ReadMessageTypeAsync(cts.Token).ConfigureAwait(false);
                if (messageType is MessageType.None)
                    break;

                cts.Timeout = ReceiveTimeout;
                await ProcessRequestAsync(messageType, protocol, cts.Token).ConfigureAwait(false);
                protocol.Reset();

                // reset cancellation token
                await cts.DisposeAsync().ConfigureAwait(false);
                cts = default; // to avoid duplicate call in finally block
            }
        }
        catch (ConnectionResetException)
        {
            // reset by client
            logger.ConnectionWasResetByClient(clientAddress);
        }
        catch (OperationCanceledException e) when (e.CausedBy(cts, connection.ConnectionClosed))
        {
            // closed by client
            logger.ConnectionWasResetByClient(clientAddress);
        }
        catch (OperationCanceledException e) when (e.CausedBy(cts, lifecycleToken))
        {
            // server stopped, suppress exception
        }
        catch (OperationCanceledException e) when (e.CausedByTimeout(cts))
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
            await protocol.DisposeAsync().ConfigureAwait(false);
            await cts.DisposeAsync().ConfigureAwait(false);
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

                    _ = HandleConnection(connection,
                        GetTransmissionSize(connection),
                        connection.RemoteEndPoint,
                        GetMemoryAllocator(connection)
                    );
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
        
        static int GetTransmissionSize(ConnectionContext connection)
            => connection.Transport.Output.GetSpan().Length;
    }

    private MemoryAllocator<byte> GetMemoryAllocator(BaseConnectionContext connection)
        => connection.Features.Get<MemoryAllocator<byte>>()
           ?? connection.Features.Get<IMemoryPoolFeature>()?.MemoryPool.ToAllocator()
           ?? MemoryAllocator;

    public override async ValueTask StartAsync(CancellationToken token)
        => listenerTask = Listen(await ListenerFactory.BindAsync(Address, token).ConfigureAwait(false));

    private void CleanUp()
    {
        using var tokenSource = Interlocked.Exchange(ref transmissionState, null);
        tokenSource?.Cancel(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CleanUp();
        }

        base.Dispose(disposing);
    }

    protected override ValueTask DisposeAsyncCore()
    {
        CleanUp();
        return new(listenerTask ?? Task.CompletedTask);
    }
}