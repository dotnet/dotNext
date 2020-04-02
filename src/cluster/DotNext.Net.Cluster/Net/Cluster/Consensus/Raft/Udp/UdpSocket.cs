using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Debug = System.Diagnostics.Debug;
using static System.Collections.Immutable.ImmutableHashSet;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    using ByteBuffer = Buffers.ArrayRental<byte>;

    internal abstract class UdpSocket : Socket
    {
        private protected interface IChannel : IDisposable
        {
            IExchange Exchange { get; }
            CancellationToken Token { get; }
        }

        private protected sealed class ChannelPool<TChannel> : ConcurrentDictionary<CorrelationId, TChannel>
            where TChannel : struct, IChannel
        {
            internal ChannelPool(int backlog)
                : base(backlog, backlog)
            {
            }

            internal void ClearAndDestroyChannels()
            {
                foreach(var channel in Values)
                    using(channel)
                        channel.Exchange.OnCanceled(new CancellationToken(true));
                Clear();
            }

            internal void CancellationRequested(object correlationId)
            {
                if(TryRemove((CorrelationId)correlationId, out var channel))
                    try
                    {
                        Debug.Assert(channel.Token.IsCancellationRequested);
                        channel.Exchange.OnCanceled(channel.Token);
                    }
                    finally
                    {
                        channel.Dispose();
                    }
            }

            internal void CancellationRequested(ref TChannel channel, CorrelationId correlationId)
            {
                if(TryRemove(correlationId, out channel))
                try
                {
                    channel.Exchange.OnException(new OperationCanceledException(ExceptionMessages.CanceledByRemoteHost));
                }
                finally
                {
                    channel.Dispose();
                }
            }

            internal void ReportError(SocketError error)
            {
                //broadcast error to all response waiters
                var e = new SocketException((int)error);
                var abortedChannels = Keys.ToImmutableHashSet();
                foreach(var id in abortedChannels)
                    if(TryRemove(id, out var channel))
                        using(channel)
                            channel.Exchange.OnException(e);
            }
        }

        private sealed class SendTask : SocketAsyncEventArgs, IValueTaskSource
        {
            private ManualResetValueTaskSourceCore<bool> taskSource;
            private readonly Action<SendTask> backToPool;

            internal SendTask(Action<SendTask> backToPool)
            {
                taskSource = new ManualResetValueTaskSourceCore<bool> { RunContinuationsAsynchronously = true };
                this.backToPool = backToPool;
            }

            void IValueTaskSource.GetResult(short token)
                => taskSource.GetResult(token);
            
            ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
                => taskSource.GetStatus(token);
            
            void IValueTaskSource.OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
                => taskSource.OnCompleted(continuation, state, token, flags);

            internal void Initialize(Memory<byte> buffer, EndPoint endPoint)
            {
                RemoteEndPoint = endPoint;
                SetBuffer(buffer);
                taskSource.Reset();
            }

            internal ValueTask Task => new ValueTask(this, taskSource.Version);

            protected override void OnCompleted(SocketAsyncEventArgs e)
            {
                if(e.SocketError == SocketError.Success)
                    taskSource.SetResult(true);
                else
                    taskSource.SetException(new SocketException((int)e.SocketError));
                backToPool(this);
            }
        }

        private sealed class SendTaskPool : ConcurrentBag<SendTask>
        {
            internal void Populate(int count)
            {
                Action<SendTask> backToPool = Add;
                for(var i = 0; i < count; i++)
                    Add(new SendTask(backToPool));
            }
        }

        internal const int MaxDatagramSize = 65507;
        internal const int MinDatagramSize = 300;

        private readonly ArrayPool<byte> bufferPool;
        internal readonly IPEndPoint Address;
        private protected readonly ILogger logger;
        //I/O management
        private readonly SocketAsyncEventArgs?[] receiverPool;
        private readonly SendTaskPool senderPool;
        private readonly Action<SocketAsyncEventArgs> dispatcher;
        private readonly int datagramSize;

        private protected UdpSocket(IPEndPoint address, int backlog, int datagramSize, ArrayPool<byte> pool, ILoggerFactory loggerFactory)
            : base(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp)
        {
            ExclusiveAddressUse = true;
            Address = address;
            Blocking = false;
            senderPool = new SendTaskPool();
            logger = loggerFactory.CreateLogger(GetType());
            bufferPool = pool;
            receiverPool = new SocketAsyncEventArgs?[backlog];
            dispatcher = BeginReceive;
            this.datagramSize = datagramSize.Between(MinDatagramSize, MaxDatagramSize, BoundType.Closed) ? 
                datagramSize 
                : throw new ArgumentOutOfRangeException(nameof(datagramSize));
        }

        private protected abstract void EndReceive(object sender, SocketAsyncEventArgs args);

        private void EndReceiveImpl(object sender, SocketAsyncEventArgs args)
        {
            if(args.SocketError == SocketError.Success)
                EndReceive(sender, args);
            else
                ReportError(args.SocketError);
        }

        private protected virtual void ReportError(SocketError error)
            => logger.SockerErrorOccurred(error);

        private protected virtual new EndPoint RemoteEndPoint => Address;

        private void BeginReceive(SocketAsyncEventArgs args)
        {
            args.RemoteEndPoint = RemoteEndPoint;
            bool result;
            try
            {
                result = ReceiveFromAsync(args);
            }
            catch(ObjectDisposedException)
            {
                args.SocketError = SocketError.Shutdown;
                result = false;
            }
            if (!result) //completed synchronously
                EndReceiveImpl(this, args);
        }

        private protected void Start(object? userToken = null)
        {
            EventHandler<SocketAsyncEventArgs> completedHandler = EndReceiveImpl;
            for(var i = 0; i < receiverPool.Length; i++)
            {
                var args = new SocketAsyncEventArgs { UserToken = userToken};
                args.SetBuffer(new byte[datagramSize]);
                args.Completed += completedHandler;
                receiverPool[i] = args;
                BeginReceive(args);
            }
            senderPool.Populate(receiverPool.Length);
        }

        internal void Stop() => Shutdown(SocketShutdown.Both);

        private protected void ProcessCancellation<TChannel, TContext>(RefAction<TChannel, TContext> action, ref TChannel channel, TContext context, SocketAsyncEventArgs args)
            where TChannel : struct, IChannel
        {
            try
            {
                action(ref channel, context);
            }
            catch(Exception e)
            {
                channel.Exchange.OnException(e);
            }
            finally
            {
                ThreadPool.QueueUserWorkItem(dispatcher, args, true);
            }
        }

        private protected async void ProcessDatagram<TChannel>(ConcurrentDictionary<CorrelationId, TChannel> channels, TChannel channel, CorrelationId correlationId, PacketHeaders headers, ReadOnlyMemory<byte> datagram, SocketAsyncEventArgs args)
            where TChannel : struct, IChannel
        {
            bool stateFlag;
            var error = default(Exception);
            var ep = args.RemoteEndPoint;
            //handle received packet
            try
            {
                stateFlag = await channel.Exchange.ProcessInbountMessageAsync(headers, datagram, ep, channel.Token).ConfigureAwait(false);
            }
            catch(Exception e)
            {
                stateFlag = false;
                error = e;
            }
            finally
            {
                //datagram buffer is no longer needed so we can return control to the event loop
                ThreadPool.QueueUserWorkItem(dispatcher, args, true);
            }
            //send one more datagram if exchange requires this
            if(stateFlag)
                try
                {
                    stateFlag = await SendAsync(correlationId, channel, ep).ConfigureAwait(false);
                }
                catch(Exception e)
                {
                    stateFlag = false;
                    error = e;
                }
            //remove exchange if it is in final state
            if(!stateFlag && channels.TryRemove(correlationId, out channel))
                using(channel)
                    if(!(error is null))
                        channel.Exchange.OnException(error);
        }

        [SuppressMessage("Reliability", "CA2000", Justification = "Task is from pool and its lifetime controlled by entire socket instance")]
        private ValueTask SendToAsync(Memory<byte> datagram, EndPoint endPoint)
        {
            //obtain sender task from the pool
            if(senderPool.TryTake(out var task))
            {
                task.Initialize(datagram, endPoint);
                if(SendToAsync(task))
                    return task.Task;
                else if(task.SocketError == SocketError.Success)
                    return new ValueTask();
                else
                    return new ValueTask(Task.FromException(new SocketException((int)task.SocketError)));
            }
            else
                return new ValueTask(Task.FromException(new InvalidOperationException(ExceptionMessages.NotEnoughSenders)));
        }

        private protected async Task<bool> SendAsync<TChannel>(CorrelationId id, TChannel channel, EndPoint endpoint)
            where TChannel : struct, IChannel
        {
            bool waitForInput;
            var bufferHolder = AllocDatagramBuffer();
            try
            {
                PacketHeaders headers;
                int bytesWritten;
                //write payload
                (headers, bytesWritten, waitForInput) = await channel.Exchange.CreateOutboundMessageAsync(AdjustToPayload(bufferHolder.Memory), channel.Token).ConfigureAwait(false);
                //write correlation ID and headers
                id.WriteTo(bufferHolder.Memory);
                headers.WriteTo(bufferHolder.Memory.Slice(CorrelationId.NaturalSize));
                await SendToAsync(AdjustDatagram(bufferHolder.Memory, bytesWritten), endpoint);
            }
            finally
            {
                bufferHolder.Dispose();
            }
            return waitForInput;
        }

        private protected ByteBuffer AllocDatagramBuffer()
            => new ByteBuffer(bufferPool, datagramSize);

        private protected static Memory<byte> AdjustToPayload(Memory<byte> datagram)
            => datagram.Slice(PacketHeaders.NaturalSize + CorrelationId.NaturalSize);
        
        private protected static Memory<byte> AdjustDatagram(Memory<byte> datagram, int payloadSize)
            => datagram.Slice(0, PacketHeaders.NaturalSize + CorrelationId.NaturalSize + payloadSize);

        private void Cleanup(bool disposing)
        {
            if(disposing)
            {
                foreach(ref var args in receiverPool.AsSpan())
                {
                    args?.Dispose();
                    args = null;
                }
                foreach(var task in senderPool)
                    task.Dispose();
                senderPool.Clear();
            }
        }

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