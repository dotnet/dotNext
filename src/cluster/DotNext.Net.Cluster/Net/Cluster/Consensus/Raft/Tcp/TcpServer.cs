using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp
{
    using TransportServices;

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

        private sealed class ServerNetworkStream : TcpStream
        {
            internal ServerNetworkStream(Socket client)
                : base(client, true)
            {
            }

            internal bool Connected => Socket.Connected;

            internal async Task<bool> Exchange(IExchangePool pool, Memory<byte> buffer, CancellationToken token)
            {
                var (headers, request) = await ReadPacket(buffer, token).ConfigureAwait(false);
                bool result;
                if(result = pool.TryRent(headers, out var exchange))
                    try
                    {
                        while(await exchange.ProcessInboundMessageAsync(headers, request, Socket.RemoteEndPoint, token).ConfigureAwait(false))
                        {
                            bool waitForInput;
                            int count;
                            (headers, count, waitForInput) = await exchange.CreateOutboundMessageAsync(AdjustToPayload(buffer), token);
                            //transmit packet to the remote endpoint
                            await WritePacket(headers, buffer, count, token).ConfigureAwait(false);
                            if(!waitForInput)
                                break;
                            //read response
                            (headers, request) = await ReadPacket(buffer, token).ConfigureAwait(false);    
                        }
                    }
                    catch(OperationCanceledException e)
                    {
                        exchange.OnCanceled(e.CancellationToken);
                        throw;
                    }
                    catch(Exception e)
                    {
                        exchange.OnException(e);
                        throw;
                    }
                    finally
                    {
                        pool.Release(exchange);
                    }
                return result;
            }
        }

        private readonly Socket socket;
        private TimeSpan receiveTimeout;
        private readonly int backlog;
        private readonly IExchangePool exchanges;

        internal TcpServer(IPEndPoint address, int backlog, ArrayPool<byte> pool, Func<int, IExchangePool> exchangePoolFactory, ILoggerFactory loggerFactory)
            : base(address, pool, loggerFactory)
        {
            socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.backlog = backlog;
            exchanges = exchangePoolFactory(backlog);
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
            while(stream.Connected)
            {
                var timeoutSource = new CancellationTokenSource(receiveTimeout);
                var buffer = AllocTransmissionBlock();
                try
                {
                    if(!await stream.Exchange(exchanges, buffer.Memory, timeoutSource.Token).ConfigureAwait(false))
                        logger.NotEnoughRequestHandlers();    
                }
                catch(OperationCanceledException e)
                {
                    if(timeoutSource.IsCancellationRequested)
                        logger.RequestTimedOut(e);
                    else
                        logger.FailedToHandleRequest(e);
                }
                catch(Exception e)
                {
                    logger.FailedToHandleRequest(e);
                }
                finally
                {
                    buffer.Dispose();
                    timeoutSource.Dispose();
                }
            }
        }

        private async void Listen()
        {
            using var args = new AcceptEventArgs();
            for(var pending = true; pending; )
                try
                {
                    if(socket.AcceptAsync(args))
                        await args.Task.ConfigureAwait(false);
                    else if(args.SocketError != SocketError.Success)
                        throw new SocketException((int)args.SocketError);
                    ConfigureSocket(args.AcceptSocket, LingerOption);
                    ThreadPool.QueueUserWorkItem(HandleConnection, args.AcceptSocket, false);
                    args.Reset();
                }
                catch(ObjectDisposedException)
                {
                    pending = false;
                }
                catch(SocketException e)
                {
                    switch(e.SocketErrorCode)
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
                catch(Exception e)
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
            if(disposing)
            {
                socket.Dispose();
                (exchanges as IDisposable)?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}