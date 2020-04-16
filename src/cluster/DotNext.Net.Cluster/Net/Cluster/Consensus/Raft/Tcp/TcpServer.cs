using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EndOfStreamException = System.IO.EndOfStreamException;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp
{
    using TransportServices;
    using static Threading.LinkedTokenSourceFactory;

    internal sealed class TcpServer : TcpTransport, IServer
    {
        private sealed class AcceptEventArgs : SocketAsyncEventSource
        {
            internal AcceptEventArgs()
                : base(true)
            {

            }

            internal override void Reset()
            {
                AcceptSocket = null;
                base.Reset();
            }
        }

        private enum ExchangeResult : byte
        {
            Success = 0,
            ExchangePoolIsEmpty,
            NoData,
            Canceled
        }

        private sealed class ServerNetworkStream : TcpStream
        {
            internal ServerNetworkStream(Socket client)
                : base(client, true)
            {
            }

            internal bool Connected => Socket.Connected;

            internal async Task<ExchangeResult> Exchange(IExchangePool pool, Memory<byte> buffer, TimeSpan receiveTimeout, CancellationToken token)
            {
                var (headers, request) = await ReadPacket(buffer, token).ConfigureAwait(false);
                var result = ExchangeResult.Success;
                if (pool.TryRent(headers, out var exchange))
                {
                    var timeoutTracker = new CancellationTokenSource(receiveTimeout);
                    var combinedSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutTracker.Token, token);
                    token = combinedSource.Token;
                    try
                    {
                        while (await exchange.ProcessInboundMessageAsync(headers, request, Socket.RemoteEndPoint, token).ConfigureAwait(false))
                        {
                            bool waitForInput;
                            int count;
                            (headers, count, waitForInput) = await exchange.CreateOutboundMessageAsync(AdjustToPayload(buffer), token);
                            //transmit packet to the remote endpoint
                            await WritePacket(headers, buffer, count, token).ConfigureAwait(false);
                            if (!waitForInput)
                                break;
                            //read response
                            (headers, request) = await ReadPacket(buffer, token).ConfigureAwait(false);
                        }
                    }
                    catch (EndOfStreamException e)
                    {
                        exchange.OnException(e);
                        result = ExchangeResult.NoData;
                    }
                    catch (OperationCanceledException e)
                    {
                        exchange.OnCanceled(e.CancellationToken);
                        result = ExchangeResult.Canceled;
                    }
                    catch (Exception e)
                    {
                        exchange.OnException(e);
                        throw;
                    }
                    finally
                    {
                        combinedSource.Dispose();
                        timeoutTracker.Dispose();
                        pool.Release(exchange);
                    }
                }
                else
                    result = ExchangeResult.ExchangePoolIsEmpty;
                return result;
            }
        }

        private readonly Socket socket;
        private TimeSpan receiveTimeout;
        private readonly int backlog;
        private readonly IExchangePool exchanges;
        private readonly CancellationTokenSource transmissionState;

        internal TcpServer(IPEndPoint address, int backlog, ArrayPool<byte> pool, Func<int, IExchangePool> exchangePoolFactory, ILoggerFactory loggerFactory)
            : base(address, pool, loggerFactory)
        {
            socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.backlog = backlog;
            exchanges = exchangePoolFactory(backlog);
            transmissionState = new CancellationTokenSource();
        }

        public TimeSpan ReceiveTimeout
        {
            get => receiveTimeout;
            set
            {
                socket.ReceiveTimeout = (int)value.TotalMilliseconds;
                receiveTimeout = value;
            }
        }

        private async void HandleConnection(Socket remoteClient)
        {
            using var stream = new ServerNetworkStream(remoteClient);
            while (stream.Connected && !IsDisposed)
            {
                var buffer = AllocTransmissionBlock();
                try
                {
                    switch (await stream.Exchange(exchanges, buffer.Memory, receiveTimeout, transmissionState.Token).ConfigureAwait(false))
                    {
                        default:
                            return;
                        case ExchangeResult.Success:
                            continue;
                        case ExchangeResult.Canceled:
                            logger.RequestTimedOut();
                            goto default;
                        case ExchangeResult.ExchangePoolIsEmpty:
                            logger.NotEnoughRequestHandlers();
                            goto default;
                    }
                }
                catch (Exception e)
                {
                    logger.FailedToHandleRequest(e);
                }
                finally
                {
                    buffer.Dispose();
                }
            }
        }

        private async void Listen()
        {
            using var args = new AcceptEventArgs();
            for (var pending = true; pending && !IsDisposed;)
                try
                {
                    if (socket.AcceptAsync(args))
                        await args.Task.ConfigureAwait(false);
                    else if (args.SocketError != SocketError.Success)
                        throw new SocketException((int)args.SocketError);
                    ConfigureSocket(args.AcceptSocket, LingerOption);
                    ThreadPool.QueueUserWorkItem(HandleConnection, args.AcceptSocket, false);
                    args.Reset();
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

        public void Start()
        {
            socket.Bind(Address);
            socket.Listen(backlog);
            Listen();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                try
                {
                    transmissionState.Cancel(false);
                }
                finally
                {
                    transmissionState.Dispose();
                    socket.Dispose();
                    (exchanges as IDisposable)?.Dispose();
                }
        }
    }
}