using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp
{
    using Threading;
    using TransportServices;
    
    /*
        This implementation doesn't support multiplexing over single TCP
        connection so CorrelationId header is not needed
    */
    internal sealed class TcpClient : TcpTransport, IClient
    {
        private sealed class ConnectEventArgs : SocketAsyncEventSource
        {
            private readonly CancellationToken token;

            internal ConnectEventArgs(CancellationToken token) : base(false) => this.token = token;

            private protected override bool IsCancellationRequested(out CancellationToken token)
            {
                token = this.token;
                return this.token.IsCancellationRequested;
            }
        }

        private sealed class ClientNetworkStream : TcpStream
        {
            internal ClientNetworkStream(Socket socket)
                : base(socket, true)
            {
            }

            internal async Task Exchange(IExchange exchange, Memory<byte> buffer, CancellationToken token)
            {
                PacketHeaders headers;
                int count;
                bool waitForInput;
                ReadOnlyMemory<byte> response;
                do
                {
                    (headers, count, waitForInput) = await exchange.CreateOutboundMessageAsync(AdjustToPayload(buffer), token);
                    //transmit packet to the remote endpoint
                    await WritePacket(headers, buffer, count, token).ConfigureAwait(false);
                    if(!waitForInput)
                        break;
                    //read response
                    (headers, response) = await ReadPacket(buffer, token).ConfigureAwait(false);
                }
                while(await exchange.ProcessInboundMessageAsync(headers, response, Socket.RemoteEndPoint, token).ConfigureAwait(false));
            }
        }
        
        private readonly AsyncExclusiveLock accessLock;
        private volatile ClientNetworkStream? stream;

        internal TcpClient(IPEndPoint address, ArrayPool<byte> pool, ILoggerFactory loggerFactory)
            : base(address, pool, loggerFactory)
        {
            accessLock = new AsyncExclusiveLock();
        }

        private static void CancelConnectAsync(object args)
            => Socket.CancelConnectAsync((SocketAsyncEventArgs)args);

        private static async Task<ClientNetworkStream> ConnectAsync(IPEndPoint endPoint, LingerOption linger, CancellationToken token)
        {
            using var args = new ConnectEventArgs(token);
            args.RemoteEndPoint = endPoint;
            if(Socket.ConnectAsync(SocketType.Stream, ProtocolType.Tcp, args))
                using(token.Register(CancelConnectAsync, args))
                {
                    await args.Task.ConfigureAwait(false);
                }
            else if(args.SocketError != SocketError.Success)
                throw new SocketException((int)args.SocketError);
            ConfigureSocket(args.ConnectSocket, linger);
            return new ClientNetworkStream(args.ConnectSocket);
        }

        private async ValueTask<ClientNetworkStream?> ConnectAsync(IExchange exchange, CancellationToken token)
        {
            ClientNetworkStream? result;
            using(await accessLock.AcquireLockAsync(token).ConfigureAwait(false))
                if(stream is null)
                    try
                    {
                        result = stream = await ConnectAsync(Address, LingerOption, token).ConfigureAwait(false);
                    }
                    catch(Exception e)
                    {
                        exchange.OnException(e);
                        result = null;
                    }
                else
                    result = stream;
            return result;
        }

        public async void Enqueue(IExchange exchange, CancellationToken token)
        {
            ThrowIfDisposed();
            var stream = this.stream;
            //establish connection if needed
            if(stream is null)
            {
                stream = await ConnectAsync(exchange, token).ConfigureAwait(false);
                if(stream is null)
                    return;
            }
            //allocate single buffer for this exchange session
            var lockHolder = await accessLock.AcquireLockAsync(token).ConfigureAwait(false);
            var buffer = AllocTransmissionBlock();
            try
            {
                await stream.Exchange(exchange, buffer.Memory, token).ConfigureAwait(false);
            }
            catch(OperationCanceledException e)
            {
                exchange.OnCanceled(e.CancellationToken);
            }
            catch(SocketException e)
            {
                stream = Interlocked.Exchange(ref this.stream, null);
                if(stream != null)
                    await stream.DisposeAsync().ConfigureAwait(false);
                exchange.OnException(e);
            }
            catch(Exception e)
            {
                exchange.OnException(e);
            }
            finally
            {
                buffer.Dispose();
                lockHolder.Dispose();
            }
        }
        
        public void CancelPendingRequests()
        {
            accessLock.CancelSuspendedCallers(new CancellationToken(true));
            Interlocked.Exchange(ref stream, null)?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                Interlocked.Exchange(ref stream, null)?.Dispose();
                accessLock.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}