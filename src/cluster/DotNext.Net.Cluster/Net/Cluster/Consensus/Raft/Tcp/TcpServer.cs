using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EndOfStreamException = System.IO.EndOfStreamException;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp
{
    using Buffers;
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

        private enum ExchangeResult : byte
        {
            Success = 0,
            SocketError,
            TimeOut,
            Stopped
        }

        private sealed class ServerNetworkStream : TcpStream
        {
            internal ServerNetworkStream(Socket client)
                : base(client, true)
            {
            }

            internal async Task<ExchangeResult> Exchange(IExchange exchange, Memory<byte> buffer, TimeSpan receiveTimeout, CancellationToken token)
            {
                var result = ExchangeResult.Success;
                CancellationTokenSource? timeoutTracker = null, combinedSource = null;
                try
                {
                    var (headers, request) = await ReadPacket(buffer, token).ConfigureAwait(false);
                    timeoutTracker = new CancellationTokenSource(receiveTimeout);
                    combinedSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutTracker.Token, token);
                    token = combinedSource.Token;
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
                catch (OperationCanceledException e)
                {
                    exchange.OnCanceled(e.CancellationToken);
                    result = !(timeoutTracker is null) && timeoutTracker.IsCancellationRequested ? 
                        ExchangeResult.TimeOut : 
                        ExchangeResult.Stopped;
                }
                catch(Exception e) when (e is EndOfStreamException || e is SocketException || e.InnerException is SocketException)
                {
                    exchange.OnException(e);
                    result = ExchangeResult.SocketError;
                }
                catch (Exception e)
                {
                    exchange.OnException(e);
                }
                finally
                {
                    combinedSource?.Dispose();
                    timeoutTracker?.Dispose();
                }
                return result;
            }
        }

        private readonly Socket socket;
        private TimeSpan receiveTimeout;
        private readonly int backlog;
        private readonly Func<IReusableExchange> exchangeFactory;
        private readonly CancellationTokenSource transmissionState;
        private volatile int connections;
        internal int GracefulShutdownTimeout;

        internal TcpServer(IPEndPoint address, int backlog, MemoryAllocator<byte> allocator, Func<IReusableExchange> exchangeFactory, ILoggerFactory loggerFactory)
            : base(address, allocator, loggerFactory)
        {
            socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.backlog = backlog;
            this.exchangeFactory = exchangeFactory;
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
            var stream = new ServerNetworkStream(remoteClient);
            var buffer = AllocTransmissionBlock();
            var exchange = exchangeFactory();
            Interlocked.Increment(ref connections);
            try
            {
                while (stream.Connected && !IsDisposed)
                    switch (await stream.Exchange(exchange, buffer.Memory, receiveTimeout, transmissionState.Token).ConfigureAwait(false))
                    {
                        default:
                            return;
                        case ExchangeResult.Success:
                            exchange.Reset();
                            continue;
                        case ExchangeResult.TimeOut:
                            remoteClient.Disconnect(false);
                            logger.RequestTimedOut();
                            goto default;
                    }
            }
            finally
            {
                buffer.Dispose();
                stream.Close(GracefulShutdownTimeout);
                stream.Dispose();
                exchange.Dispose();
                Interlocked.Decrement(ref connections);
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

        private bool NoMoreConnections() => connections <= 0;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                try
                {
                    transmissionState.Cancel(false);
                }
                finally
                {
                    transmissionState.Dispose();
                    socket.Close(GracefulShutdownTimeout);
                    socket.Dispose();
                }
                if(!SpinWait.SpinUntil(NoMoreConnections, GracefulShutdownTimeout))
                    logger.TcpGracefulShutdownFailed(GracefulShutdownTimeout);
            }
        }
    }
}