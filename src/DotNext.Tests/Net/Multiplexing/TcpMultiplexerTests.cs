using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.IO.Pipelines;
using System.Net;

namespace DotNext.Net.Multiplexing;

using Diagnostics.Metrics;
using IO.Pipelines;

[Experimental("DOTNEXT001")]
public sealed class TcpMultiplexerTests : Test
{
    private static readonly IPEndPoint LocalEndPoint = new(IPAddress.Loopback, 3280);

    [Fact]
    public static async Task SuccessfulDataExchange()
    {
        await using var server = new TcpMultiplexedListener(LocalEndPoint, new() { Timeout = DefaultTimeout });
        await server.StartAsync();

        await using var client = new TcpMultiplexedClient(LocalEndPoint, new() { Timeout = DefaultTimeout });
        await client.StartAsync();

        var clientStream1 = await client.OpenStreamAsync();
        var clientStream2 = await client.OpenStreamAsync();

        var expectedData1 = RandomBytes(1024);
        var expectedData2 = RandomBytes(1024);

        var result = await clientStream1.Output.WriteAsync(expectedData1);
        False(result.IsCanceled);
        False(result.IsCompleted);
        
        result = await clientStream2.Output.WriteAsync(expectedData2);
        False(result.IsCompleted);
        False(result.IsCanceled);

        var serverStream1 = await server.AcceptAsync();
        var serverStream2 = await server.AcceptAsync();

        var actualData1 = new byte[expectedData1.Length];
        await serverStream1.Input.ReadExactlyAsync(actualData1);
        
        var actualData2 = new byte[expectedData2.Length];
        await serverStream2.Input.ReadExactlyAsync(actualData2);

        Equal(expectedData1, actualData1);
        Equal(expectedData2, actualData2);
    }

    [Fact]
    public static async Task LargeDataExchangeAsync()
    {
        await using var server = new TcpMultiplexedListener(LocalEndPoint, new()
        {
            Timeout = DefaultTimeout
        });
        await server.StartAsync();

        await using var client = new TcpMultiplexedClient(LocalEndPoint, new()
        {
            Timeout = DefaultTimeout
        });
        await client.StartAsync();

        var expectedData = RandomBytes(1024 * 1024);

        await Task.WhenAll(SendAsync(), ReceiveAsync());

        async Task SendAsync()
        {
            var stream = await client.OpenStreamAsync();
            await stream.Output.WriteAsync(expectedData);
            await stream.Output.CompleteAsync();
            await stream.Input.CompleteAsync();
        }

        async Task ReceiveAsync()
        {
            var stream = await server.AcceptAsync();
            var offset = 0;
            var actualData = new byte[expectedData.Length];
            
            await foreach (var block in stream.Input.ReadAllAsync())
            {
                block.CopyTo(actualData.AsMemory(offset));
                offset += block.Length;
            }

            Equal(expectedData, actualData);
        }
    }

    [Fact]
    public static async Task HeartbeatAsync()
    {
        var timeout = TimeSpan.FromSeconds(1);
        await using var server = new TcpMultiplexedListener(LocalEndPoint, new()
        {
            Timeout = timeout,
            BufferOptions = PipeOptions.Default,
            Backlog = 3,
        });
        await server.StartAsync();
        
        await using var client = new TcpMultiplexedClient(LocalEndPoint, new() { Timeout = timeout });
        await client.StartAsync();

        var clientStream = await client.OpenStreamAsync();

        // no traffic for a long time
        await Task.Delay(timeout * 2);
        
        // ensure that the client is alive
        var expectedData = RandomBytes(1024);
        var flushResult = await clientStream.Output.WriteAsync(expectedData);
        await clientStream.Output.CompleteAsync();
        False(flushResult.IsCompleted);
        False(flushResult.IsCanceled);

        var serverStream = await server.AcceptAsync();
        
        var actualData = new byte[expectedData.Length];
        var offset = 0;
        await foreach (var block in serverStream.Input.ReadAllAsync())
        {
            block.CopyTo(actualData.AsMemory(offset));
            offset += block.Length;
        }

        Equal(expectedData, actualData);
    }

    [Fact]
    public static async Task RequestResponseAsync()
    {
        await using var server = new TcpMultiplexedListener(LocalEndPoint, new() { Timeout = DefaultTimeout });
        await server.StartAsync();

        await using var client = new TcpMultiplexedClient(LocalEndPoint, new() { Timeout = DefaultTimeout });
        await client.StartAsync();
        var expectedData = RandomBytes(1024);

        var serverTask = PongPingServer();
        var clientTask = PingPongClient(await client.OpenStreamAsync());

        await Task.WhenAll(clientTask, serverTask);

        async Task PingPongClient(IDuplexPipe stream)
        {
            var result = await stream.Output.WriteAsync(expectedData);
            False(result.IsCompleted);
            False(result.IsCanceled);

            await stream.Output.CompleteAsync();

            var actualData = new byte[expectedData.Length];
            var offset = 0;
            await foreach (var block in stream.Input.ReadAllAsync())
            {
                block.CopyTo(actualData.AsMemory(offset));
                offset += block.Length;
            }

            Equal(expectedData, actualData);
        }

        async Task PongPingServer()
        {
            var stream = await server.AcceptAsync();
            var actualData = new byte[expectedData.Length];
            var offset = 0;
            await foreach (var block in stream.Input.ReadAllAsync())
            {
                block.CopyTo(actualData.AsMemory(offset));
                offset += block.Length;
            }

            Equal(expectedData, actualData);

            var result = await stream.Output.WriteAsync(expectedData);
            False(result.IsCompleted);
            False(result.IsCanceled);

            await stream.Output.CompleteAsync();
        }
    }

    [Fact]
    public static async Task TerminateStream()
    {
        var streamCount = new StreamCountObserver();
        using var listener = new MeterListenerBuilder()
            .Observe(StreamCountObserver.IsStreamCount, streamCount)
            .Build();
        
        listener.Start();

        await using var server = new TcpMultiplexedListener(LocalEndPoint, new() { Timeout = DefaultTimeout, MeasurementTags = new() });
        await server.StartAsync();

        await using var client = new TcpMultiplexedClient(LocalEndPoint, new() { Timeout = DefaultTimeout, MeasurementTags = new() });
        await client.StartAsync();

        var clientStream = await client.OpenStreamAsync();
        await clientStream.Input.CompleteAsync(new ArithmeticException());
        await clientStream.Output.WriteAsync(new byte[3]);

        var serverStream = await server.AcceptAsync();
        await clientStream.Output.CompleteAsync(new ArithmeticException());

        ReadResult result;
        do
        {
            result = await serverStream.Input.ReadAsync();
            serverStream.Input.AdvanceTo(result.Buffer.End);
        } while (!result.IsCompleted);
        
        await serverStream.Input.CompleteAsync();
        await serverStream.Output.CompleteAsync();

        await streamCount.WaitForZero(DefaultTimeout);
    }

    private sealed class StreamCountObserver() : InstrumentObserver<long, UpDownCounter<long>>(static (instr, tags) => IsStreamCount(instr))
    {
        private readonly TaskCompletionSource zeroReached = new();
        private long streamCount;

        internal static bool IsStreamCount(Instrument instrument)
            => instrument is { Meter.Name: "DotNext.Net.Multiplexing.Server", Name: "streams-count" };

        protected override void Record(long value)
        {
            if (Interlocked.Add(ref streamCount, value) is 0)
                zeroReached.TrySetResult();
        }

        public Task WaitForZero(TimeSpan timeout) => zeroReached.Task.WaitAsync(timeout);
    }

    [Fact]
    public static async Task WaitForConnectionAsync()
    {
        await using var client = new TcpMultiplexedClient(LocalEndPoint, new() { Timeout = DefaultTimeout });
        
        await using var server = new TcpMultiplexedListener(LocalEndPoint, new() { Timeout = DefaultTimeout });
        await server.StartAsync();

        var task = client.OpenStreamAsync().AsTask();
        False(task.IsCompleted);

        await client.StartAsync();
        await task;
        True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public static async Task WaitForDisposedConnectionAsync()
    {
        Task task;
        await using (var client = new TcpMultiplexedClient(LocalEndPoint, new() { Timeout = DefaultTimeout }))
        {
            task = client.OpenStreamAsync().AsTask();
        }

        await ThrowsAsync<ObjectDisposedException>(Func.Constant(task));
    }
    
    [Fact]
    public static async Task WaitForCanceledConnectionAsync()
    {
        await using var client = new TcpMultiplexedClient(LocalEndPoint, new() { Timeout = DefaultTimeout });
        await ThrowsAnyAsync<OperationCanceledException>(client.OpenStreamAsync(new CancellationToken(canceled: true)).AsTask);
    }
}