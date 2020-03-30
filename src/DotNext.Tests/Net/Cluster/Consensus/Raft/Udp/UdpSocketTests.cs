using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    public sealed class UdpSocketTests : Test
    {
        private sealed class VoteResponse : Assert, IExchange
        {
            private readonly Action<object> errorCallback;

            internal VoteResponse(Action<object> errorCallback)
                => this.errorCallback = errorCallback;

            public ValueTask<bool> ProcessInbountMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, EndPoint endPoint, CancellationToken token)
            {
                Equal(MessageType.Vote, headers.Type);
                Equal(FlowControl.None, headers.Control);
                NotNull(endPoint);
                True(token.CanBeCanceled);
                Equal(42L, headers.Term);
                VoteExchange.Parse(payload.Span, out var lastLogIndex, out var lastLogTerm);
                Equal(1L, lastLogIndex);
                Equal(56L, lastLogTerm);
                return new ValueTask<bool>(true);
            }

            public ValueTask<(PacketHeaders Headers, int BytesWritten, bool)> CreateOutboundMessageAsync(Memory<byte> buffer, CancellationToken token)
            {
                True(token.CanBeCanceled);
                buffer.Span[0] = 1;
                return new ValueTask<(PacketHeaders Headers, int BytesWritten, bool)>((new PacketHeaders(MessageType.Vote, FlowControl.Ack, 43L), 1, false));
            }
    
            public void OnException(Exception e) => errorCallback?.Invoke(e);

            public void OnCanceled(CancellationToken token) => errorCallback?.Invoke(token);
        }

        private sealed class SimpleServerExchangePool : IExchangePool
        {
            private readonly Action<object> errorCallback;

            internal SimpleServerExchangePool(Action<object> errorCallback)
                => this.errorCallback = errorCallback;

            public bool TryRent(PacketHeaders headers, out IExchange exchange)
            {
                switch(headers.Type)
                {
                    case MessageType.Vote:
                        exchange = new VoteResponse(errorCallback);
                        return true;
                    default:
                        exchange = default;
                        return false;
                }
            }

            void IExchangePool.Release(IExchange exchange) { }
        }

        [Fact]
        public static async Task ConnectionError()
        {
            using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 35666), 2, UdpSocket.MaxDatagramSize, ArrayPool<byte>.Shared, NullLoggerFactory.Instance);
            client.Start();
            var exchange = new VoteExchange(10L, 20L, 30L);
            client.Enqueue(exchange, CancellationToken.None);
            var error = await ThrowsAsync<SocketException>(() => exchange.Task);
            Equal(SocketError.ConnectionRefused, error.SocketErrorCode);
            client.Stop();
        }

        private sealed class ServerCallback
        {
            internal object Error { get; private set; }

            internal void SetError(object value) => Error = value;
        }

        [Fact]
        public static async Task ClientServerMessaging()
        {
            var timeout = TimeSpan.FromSeconds(20);
            var callback = new ServerCallback();
            //prepare server
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            using var server = new UdpServer(serverAddr, 2, UdpSocket.MaxDatagramSize, ArrayPool<byte>.Shared, NullLoggerFactory.Instance);
            server.ReceiveTimeout = timeout;
            server.Start(new SimpleServerExchangePool(callback.SetError));
            //prepare client
            using var client = new UdpClient(serverAddr, 2, UdpSocket.MaxDatagramSize, ArrayPool<byte>.Shared, NullLoggerFactory.Instance);
            using var timeoutTokenSource = new CancellationTokenSource(timeout);
            client.Start();
            var exchange = new VoteExchange(42L, 1L, 56L);
            client.Enqueue(exchange, timeoutTokenSource.Token);
            var result = await exchange.Task;
            Null(callback.Error);
            True(result.Value);
            Equal(43L, result.Term);
            client.Stop();
        }

        [Fact]
        public static async Task StressTest()
        {
            var timeout = TimeSpan.FromSeconds(20);
            //prepare server
            var serverAddr = new IPEndPoint(IPAddress.Loopback, 3789);
            using var server = new UdpServer(serverAddr, 100, UdpSocket.MaxDatagramSize, ArrayPool<byte>.Shared, NullLoggerFactory.Instance);
            server.ReceiveTimeout = timeout;
            server.Start(new SimpleServerExchangePool(null));
            //prepare client
            using var client = new UdpClient(serverAddr, 100, UdpSocket.MaxDatagramSize, ArrayPool<byte>.Shared, NullLoggerFactory.Instance);
            client.Start();
            ICollection<Task<Result<bool>>> tasks = new LinkedList<Task<Result<bool>>>();
            using(var timeoutTokenSource = new CancellationTokenSource(timeout))
            {
                for(var i = 0; i < 100; i++)
                {
                    var exchange = new VoteExchange(42L, 1L, 56L);
                    client.Enqueue(exchange, timeoutTokenSource.Token);
                    tasks.Add(exchange.Task);
                }
                await Task.WhenAll(tasks);
            }
            foreach(var task in tasks)
            {
                True(task.Result.Value);
                Equal(43L, task.Result.Term);
            }
            client.Stop();
        }
    }
}