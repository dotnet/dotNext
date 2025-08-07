using System.Diagnostics.Metrics;
using System.IO.Pipelines;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DotNext.Net.Multiplexing;

using IO.Pipelines;

public sealed class TcpMultiplexerTests : Test
{
    private static readonly IPEndPoint LocalEndPoint = new(IPAddress.Loopback, 3280);

    [Fact]
    public static async Task SuccessfulDataExchange()
    {
        await using var server = new TcpMultiplexedListener(LocalEndPoint, new() { Timeout = DefaultTimeout, FragmentSize = 1024 });
        await server.StartAsync();

        await using var client = new TcpMultiplexedClient(LocalEndPoint, new() { Timeout = DefaultTimeout, FragmentSize = 1024 });
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
    public static async Task HeartbeatAsync()
    {
        var timeout = TimeSpan.FromSeconds(1);
        await using var server = new TcpMultiplexedListener(LocalEndPoint, new() { Timeout = timeout });
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
    public static async Task AbortStream()
    {
        var callback = new StreamCountListener();
        using var listener = new MeterListener();
        listener.SetMeasurementEventCallback<int>(callback.Accept);
        listener.InstrumentPublished += StreamCountListener.OnRegistered;
        listener.Start();
        
        await using var server = new TcpMultiplexedListener(LocalEndPoint, new() { Timeout = Timeout.InfiniteTimeSpan });
        await server.StartAsync();

        await using var client = new TcpMultiplexedClient(LocalEndPoint, new() { Timeout = Timeout.InfiniteTimeSpan });
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

        await callback.Task.WaitAsync(DefaultTimeout);
    }
    
    private sealed class StreamCountListener : TaskCompletionSource
    {
        private int streamCount;

        public void Accept(Instrument instrument, int measurement, ReadOnlySpan<KeyValuePair<string, object>> tags, object state)
        {
            if (IsStreamCount(instrument))
            {
                if (Interlocked.Add(ref streamCount, measurement) is 0)
                    TrySetResult();
            }
        }

        private static bool IsStreamCount(Instrument instrument)
            => instrument is { Meter.Name: "DotNext.Net.Multiplexing.Server", Name: "streams-count" };

        public static void OnRegistered(Instrument instrument, MeterListener listener)
        {
            if (IsStreamCount(instrument))
                listener.EnableMeasurementEvents(instrument);
        }
    }
}