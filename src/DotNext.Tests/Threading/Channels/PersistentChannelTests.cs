using System.Threading.Channels;

namespace DotNext.Threading.Channels;

using static Collections.Generic.AsyncEnumerable;
using static IO.StreamExtensions;

public sealed class PersistentChannelTests : Test
{
    private sealed class SerializationChannel<T> : PersistentChannel<T, T>
        where T : unmanaged
    {
        internal SerializationChannel(PersistentChannelOptions options)
            : base(options)
        {
        }

        protected override ValueTask<T> DeserializeAsync(Stream input, CancellationToken token)
            => input.ReadAsync<T>(token);

        protected override ValueTask SerializeAsync(T input, Stream output, CancellationToken token)
            => output.WriteAsync<T>(input, token);
    }

    [Theory]
    [InlineData(false, false, 0L)]
    [InlineData(false, true, 0L)]
    [InlineData(true, false, 0L)]
    [InlineData(true, true, 0L)]
    [InlineData(false, false, 10240)]
    [InlineData(false, true, 10240)]
    [InlineData(true, false, 10240)]
    [InlineData(true, true, 10240)]
    public static async Task ReadWrite(bool singleReader, bool singleWriter, long initialSize)
    {
        Guid g1 = Guid.NewGuid(), g2 = Guid.NewGuid(), g3 = Guid.NewGuid();
        using var channel = new SerializationChannel<Guid>(new PersistentChannelOptions { SingleReader = singleReader, SingleWriter = singleWriter, InitialPartitionSize = initialSize });
        False(channel.Writer.TryWrite(g1));
        await channel.Writer.WriteAsync(g1);
        await channel.Writer.WriteAsync(g2);
        await channel.Writer.WriteAsync(g3);
        Equal(g1, await channel.Reader.ReadAsync());
        Equal(g2, await channel.Reader.ReadAsync());
        True(await channel.Reader.WaitToReadAsync());
        True(channel.Reader.TryRead(out var last));
        Equal(g3, last);
        Equal(1D, channel.Throughput);
    }

    [Fact]
    public static async Task Persistence()
    {
        Guid g1 = Guid.NewGuid(), g2 = Guid.NewGuid(), g3 = Guid.NewGuid();
        var options = new PersistentChannelOptions { BufferSize = 1024 };
        using (var channel = new SerializationChannel<Guid>(options))
        {
            await channel.Writer.WriteAsync(g1);
            await channel.Writer.WriteAsync(g2);
            await channel.Writer.WriteAsync(g3);
            Equal(g1, await channel.Reader.ReadAsync());
        }
        using (var channel = new SerializationChannel<Guid>(options))
        {
            Equal(2L, channel.RemainingCount);
            Equal(g2, await channel.Reader.ReadAsync());
            Equal(g3, await channel.Reader.ReadAsync());
        }
    }

    [Fact]
    public static async Task PartitionOverflow()
    {
        var options = new PersistentChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
            PartitionCapacity = 3
        };
        Guid g1 = Guid.NewGuid(), g2 = Guid.NewGuid(), g3 = Guid.NewGuid(), g4 = Guid.NewGuid();
        using var channel = new SerializationChannel<Guid>(options);
        await channel.Writer.WriteAsync(g1);
        await channel.Writer.WriteAsync(g2);
        await channel.Writer.WriteAsync(g3);
        await channel.Writer.WriteAsync(g4);
        Equal(g1, await channel.Reader.ReadAsync());
        Equal(g2, await channel.Reader.ReadAsync());
        Equal(g3, await channel.Reader.ReadAsync());
        Equal(g4, await channel.Reader.ReadAsync());
    }

    [Fact]
    public static async Task PersistentPartitionOverflow()
    {
        var options = new PersistentChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
            PartitionCapacity = 3
        };
        Guid g1 = Guid.NewGuid(), g2 = Guid.NewGuid(), g3 = Guid.NewGuid(), g4 = Guid.NewGuid();
        using (var channel = new SerializationChannel<Guid>(options))
        {
            await channel.Writer.WriteAsync(g1);
            await channel.Writer.WriteAsync(g2);
            await channel.Writer.WriteAsync(g3);
            await channel.Writer.WriteAsync(g4);
            Equal(0D, channel.Throughput);
            Equal(g1, await channel.Reader.ReadAsync());
            Equal(0.25D, channel.Throughput);
        }
        using (var channel = new SerializationChannel<Guid>(options))
        {
            Equal(0.25D, channel.Throughput);
            Equal(g2, await channel.Reader.ReadAsync());
            Equal(0.5D, channel.Throughput);
            Equal(g3, await channel.Reader.ReadAsync());
            Equal(0.75D, channel.Throughput);
            Equal(g4, await channel.Reader.ReadAsync());
            Equal(1D, channel.Throughput);
        }
    }

    private static async Task Produce(ChannelWriter<decimal> writer, decimal startInclusive, decimal endExclusive)
    {
        for (decimal i = startInclusive; i < endExclusive; i++)
            await writer.WriteAsync(i);
    }

    private static async Task Consume(ChannelReader<decimal> reader, decimal startInclusive, decimal endExclusive)
    {
        for (decimal i = startInclusive; i < endExclusive; i++)
            Equal(i, await reader.ReadAsync());
    }

    private static async Task ConsumeInRange(ChannelReader<decimal> reader)
    {
        const decimal LowerBound = 0M;
        const decimal UpperBound = 500M;
        for (decimal i = LowerBound; i < UpperBound; i++)
            True((await reader.ReadAsync()).IsBetween(LowerBound, UpperBound, BoundType.LeftClosed));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(102400L)]
    public static async Task ProduceConsumeConcurrently(long initialSize)
    {
        using var channel = new SerializationChannel<decimal>(new PersistentChannelOptions { SingleReader = true, SingleWriter = true, PartitionCapacity = 100, InitialPartitionSize = initialSize });
        var consumer = Consume(channel.Reader, 0M, 500M);
        var producer = Produce(channel.Writer, 0M, 500M);
        await Task.WhenAll(consumer, producer);
    }

    [Theory]
    [InlineData(0L, true)]
    [InlineData(102400L, true)]
    [InlineData(0L, false)]
    [InlineData(102400L, false)]
    public static async Task ProduceConsumeInParallel(long initialSize, bool singleReader)
    {
        using var channel = new SerializationChannel<decimal>(new PersistentChannelOptions { SingleReader = singleReader, SingleWriter = false, PartitionCapacity = 100, InitialPartitionSize = initialSize });
        var consumer = ConsumeInRange(channel.Reader);
        var producer1 = Produce(channel.Writer, 0M, 250M);
        var producer2 = Produce(channel.Writer, 250M, 500M);
        await Task.WhenAll(consumer, producer1, producer2);
    }

    [Theory]
    [InlineData(false, false, 0L)]
    [InlineData(false, true, 0L)]
    [InlineData(true, false, 0L)]
    [InlineData(true, true, 0L)]
    [InlineData(false, false, 10240)]
    [InlineData(false, true, 10240)]
    [InlineData(true, false, 10240)]
    [InlineData(true, true, 10240)]
    public static async Task ChannelCompletion(bool singleReader, bool singleWriter, long initialSize)
    {
        Guid g1 = Guid.NewGuid(), g2 = Guid.NewGuid(), g3 = Guid.NewGuid();
        using var channel = new SerializationChannel<Guid>(new PersistentChannelOptions { SingleReader = singleReader, SingleWriter = singleWriter, InitialPartitionSize = initialSize });
        await channel.Writer.WriteAsync(g1);
        await channel.Writer.WriteAsync(g2);
        await channel.Writer.WriteAsync(g3);
        True(channel.Writer.TryComplete());
        await ThrowsAsync<ChannelClosedException>(channel.Writer.WriteAsync(Guid.Empty).AsTask);

        True(channel.Reader.Completion.IsCompletedSuccessfully);
        Equal(g1, await channel.Reader.ReadAsync());
        Equal(g2, await channel.Reader.ReadAsync());
        True(await channel.Reader.WaitToReadAsync());
        True(channel.Reader.TryRead(out var last));
        Equal(g3, last);
        False(await channel.Reader.WaitToReadAsync());
        await ThrowsAsync<ChannelClosedException>(channel.Reader.ReadAsync().AsTask);
    }

    [Fact]
    public static async Task ReliableEnumeration()
    {
        Guid g1 = Guid.NewGuid(), g2 = Guid.NewGuid(), g3 = Guid.NewGuid();
        using var channel = new SerializationChannel<Guid>(new PersistentChannelOptions { ReliableEnumeration = true });

        await channel.Writer.WriteAsync(g1);
        await channel.Writer.WriteAsync(g2);
        await channel.Writer.WriteAsync(g3);
        True(channel.Writer.TryComplete());

        await using (var enumerator = channel.Reader.ReadAllAsync().GetAsyncEnumerator())
        {
            True(await enumerator.MoveNextAsync());
            Equal(g1, enumerator.Current);

            True(await enumerator.MoveNextAsync());
        }

        await using (var enumerator = channel.Reader.ReadAllAsync().GetAsyncEnumerator())
        {
            True(await enumerator.MoveNextAsync());
            Equal(g2, enumerator.Current);

            True(await enumerator.MoveNextAsync());
            Equal(g3, enumerator.Current);

            False(await enumerator.MoveNextAsync());
        }
    }

    [Fact]
    public static async Task RegressionIssue136()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using (var channel = new SerializationChannel<decimal>(new PersistentChannelOptions
        { Location = path, PartitionCapacity = 3 }))
        {
            await Produce(channel.Writer, 0M, 30M);
        }

        using (var channel = new SerializationChannel<decimal>(new PersistentChannelOptions
        { Location = path, PartitionCapacity = 3 }))
        {
            await Produce(channel.Writer, 30M, 60M);
        }

        using (var channel = new SerializationChannel<decimal>(new PersistentChannelOptions
        { Location = path, PartitionCapacity = 3 }))
        {
            channel.Writer.Complete();
            await Consume(channel.Reader);
        }

        static async Task Produce(ChannelWriter<decimal> writer, decimal start, decimal end)
        {
            for (decimal i = start; i < end; i++)
            {
                await writer.WriteAsync(i);
            }
        }

        static async Task Consume(ChannelReader<decimal> reader)
        {
            var array = await reader.ReadAllAsync().ToArrayAsync();
            Equal(60, array.Length);

            for (var i = 0; i < array.Length; i++)
                Equal(new decimal(i), array[i]);
        }
    }

    [Fact]
    public static async Task ReentrantConsumption()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await ProduceTestSet(path, 0M, 1000M);
        await ProduceTestSet(path, 1000M, 2000M);
        await ProduceTestSet(path, 2000M, 3000M);

        await ConsumeTestSet(path, 0M, 3000M);

        static async Task ProduceTestSet(string path, decimal startInclusive, decimal endExclusive)
        {
            using (var channel = new SerializationChannel<decimal>(new PersistentChannelOptions { Location = path }))
            {
                await Produce(channel.Writer, startInclusive, endExclusive);
            }
        }

        static async Task ConsumeTestSet(string path, decimal startInclusive, decimal endExclusive)
        {
            using (var channel = new SerializationChannel<decimal>(new PersistentChannelOptions { Location = path }))
            {
                await Consume(channel.Reader, startInclusive, endExclusive);
            }
        }
    }
}