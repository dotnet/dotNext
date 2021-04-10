using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    using Buffers;
    using TransportServices;

    internal abstract class UdpSocket : Socket, INetworkTransport
    {
        private protected static readonly IPEndPoint AnyRemoteEndpoint = new IPEndPoint(IPAddress.Any, 0);

        private sealed class SendEventArgs : SocketAsyncEventSource
        {
            private readonly Action<SendEventArgs> backToPool;

            internal SendEventArgs(Action<SendEventArgs> backToPool)
                : base(true) => this.backToPool = backToPool;

            internal void Initialize(Memory<byte> buffer, EndPoint endPoint)
            {
                RemoteEndPoint = endPoint;
                SetBuffer(buffer);
                Reset();
            }

            internal ValueTask GetTask(bool pending)
            {
                if (pending)
                {
                    return Task;
                }
                else
                {
                    var error = SocketError;
                    backToPool?.Invoke(this);
                    return error == SocketError.Success ?
                        new () :
                        new (System.Threading.Tasks.Task.FromException(new SocketException((int)error)));
                }
            }

            protected override void OnCompleted(SocketAsyncEventArgs e)
            {
                base.OnCompleted(e);
                backToPool(this);
            }
        }

        private sealed class SendTaskPool : ConcurrentBag<SendEventArgs>
        {
            internal void Populate(int count)
            {
                Action<SendEventArgs> backToPool = Add;
                for (var i = 0; i < count; i++)
                    Add(new SendEventArgs(backToPool));
            }
        }

        internal const int MaxDatagramSize = 65507;
        internal const int MinDatagramSize = 300;
        private protected readonly MemoryAllocator<byte> allocator;
        internal readonly IPEndPoint Address;
        private protected readonly ILogger logger;

        // I/O management
        private readonly SocketAsyncEventArgs?[] receiverPool;
        private readonly SendTaskPool senderPool;
        private readonly Action<SocketAsyncEventArgs> dispatcher;
        private int datagramSize;

        private protected UdpSocket(IPEndPoint address, int backlog, MemoryAllocator<byte> allocator, ILoggerFactory loggerFactory)
            : base(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp)
        {
            ExclusiveAddressUse = true;
            Blocking = false;
            Address = address;
            logger = loggerFactory.CreateLogger(GetType());
            this.allocator = allocator;
            senderPool = new SendTaskPool();
            receiverPool = new SocketAsyncEventArgs?[backlog];
            dispatcher = BeginReceive;
            datagramSize = MinDatagramSize;
        }

        IPEndPoint INetworkTransport.Address => Address;

        internal static int ValidateDatagramSize(int value)
            => value.Between(MinDatagramSize, MaxDatagramSize, BoundType.Closed) ? value : throw new ArgumentOutOfRangeException(nameof(value));

        internal int DatagramSize
        {
            get => datagramSize;
            set => datagramSize = ValidateDatagramSize(value);
        }

        private protected abstract void EndReceive(SocketAsyncEventArgs args);

        private void EndReceive(object? sender, SocketAsyncEventArgs args)
        {
            switch (args.SocketError)
            {
                default:
                    ReportError(args.SocketError);
                    break;
                case SocketError.OperationAborted or SocketError.ConnectionAborted:
                    break;
                case SocketError.Success:
                    EndReceive(args);
                    break;
            }
        }

        private protected void ProcessCancellation<TChannel, TContext>(RefAction<TChannel, TContext> action, ref TChannel channel, TContext context, SocketAsyncEventArgs args)
            where TChannel : struct, INetworkTransport.IChannel
        {
            try
            {
                action(ref channel, context);
            }
            catch (Exception e)
            {
                channel.Exchange.OnException(e);
            }
            finally
            {
                ThreadPool.QueueUserWorkItem(dispatcher, args, true);
            }
        }

        private protected virtual void ReportError(SocketError error)
            => logger.SockerErrorOccurred(error);

        private protected abstract bool AllowReceiveFromAnyHost { get; }

        private void BeginReceive(SocketAsyncEventArgs args)
        {
            args.RemoteEndPoint = AllowReceiveFromAnyHost ? AnyRemoteEndpoint : Address;
            bool result;
            try
            {
                result = ReceiveFromAsync(args);
            }
            catch (ObjectDisposedException)
            {
                args.SocketError = SocketError.Shutdown;
                result = false;
            }

            if (!result) // completed synchronously
                EndReceive(this, args);
        }

        private protected void Start(object? userToken = null)
        {
            EventHandler<SocketAsyncEventArgs> completedHandler = EndReceive;
            for (var i = 0; i < receiverPool.Length; i++)
            {
                var args = new SocketAsyncEventArgs { UserToken = userToken };
                args.SetBuffer(new byte[datagramSize]);
                args.Completed += completedHandler;
                receiverPool[i] = args;
                BeginReceive(args);
            }

            senderPool.Populate(receiverPool.Length);
        }

        private protected async void ProcessDatagram<TChannel>(ConcurrentDictionary<CorrelationId, TChannel> channels, TChannel channel, CorrelationId correlationId, PacketHeaders headers, ReadOnlyMemory<byte> datagram, SocketAsyncEventArgs args)
            where TChannel : struct, INetworkTransport.IChannel
        {
            bool stateFlag;
            var error = default(Exception);
            var ep = args.RemoteEndPoint;
            Debug.Assert(ep is not null);

            // handle received packet
            try
            {
                stateFlag = await channel.Exchange.ProcessInboundMessageAsync(headers, datagram, ep, channel.Token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                stateFlag = false;
                error = e;
            }
            finally
            {
                // datagram buffer is no longer needed so we can return control to the event loop
                ThreadPool.QueueUserWorkItem(dispatcher, args, true);
            }

            // send one more datagram if exchange requires this
            if (stateFlag)
            {
                try
                {
                    stateFlag = await SendAsync(correlationId, channel, ep).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    stateFlag = false;
                    error = e;
                }
            }

            // remove exchange if it is in final state
            if (!stateFlag && channels.TryRemove(correlationId, out channel))
            {
                using (channel)
                {
                    if (error is not null)
                        channel.Exchange.OnException(error);
                }
            }
        }

        private ValueTask SendToAsync(Memory<byte> datagram, EndPoint endPoint)
        {
            // obtain sender task from the pool
            if (senderPool.TryTake(out var task))
            {
                task.Initialize(datagram, endPoint);
                return task.GetTask(SendToAsync(task));
            }

            return new (Task.FromException(new InvalidOperationException(ExceptionMessages.NotEnoughSenders)));
        }

        private protected async Task<bool> SendAsync<TChannel>(CorrelationId id, TChannel channel, EndPoint endpoint)
            where TChannel : struct, INetworkTransport.IChannel
        {
            bool waitForInput;
            var bufferHolder = AllocDatagramBuffer();
            try
            {
                PacketHeaders headers;
                int bytesWritten;

                // write payload
                (headers, bytesWritten, waitForInput) = await channel.Exchange.CreateOutboundMessageAsync(AdjustToPayload(bufferHolder.Memory), channel.Token).ConfigureAwait(false);

                // write correlation ID and headers
                id.WriteTo(bufferHolder.Memory.Span);
                headers.WriteTo(bufferHolder.Memory.Slice(CorrelationId.NaturalSize));
                await SendToAsync(AdjustPacket(bufferHolder.Memory, bytesWritten), endpoint).ConfigureAwait(false);
            }
            finally
            {
                bufferHolder.Dispose();
            }

            return waitForInput;
        }

        private protected MemoryOwner<byte> AllocDatagramBuffer()
            => allocator(datagramSize);

        private void Cleanup(bool disposing)
        {
            if (disposing)
            {
                foreach (ref var args in receiverPool.AsSpan())
                {
                    args?.Dispose();
                    args = null;
                }

                foreach (var task in senderPool)
                    task.Dispose();
                senderPool.Clear();
            }
        }

        private protected static Memory<byte> AdjustToPayload(Memory<byte> packet)
            => packet.Slice(PacketHeaders.NaturalSize + CorrelationId.NaturalSize);

        private protected static Memory<byte> AdjustPacket(Memory<byte> packet, int payloadSize)
            => packet.Slice(0, PacketHeaders.NaturalSize + CorrelationId.NaturalSize + payloadSize);

        protected override void Dispose(bool disposing)
        {
            try
            {
                base.Dispose(disposing);
            }
            finally
            {
                Cleanup(disposing);
            }
        }
    }
}