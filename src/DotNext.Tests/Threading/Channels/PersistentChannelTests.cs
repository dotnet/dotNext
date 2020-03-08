using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading.Channels
{
    [ExcludeFromCodeCoverage]
    public sealed class PersistentChannelTests : Test
    {
        private sealed class SerializationChannel<T> : PersistentChannel<T, T>
        {
            private readonly IFormatter formatter;

            internal SerializationChannel(PersistentChannelOptions options)
                : base(options)
            {
                formatter = new BinaryFormatter();
            }

            protected override ValueTask<T> DeserializeAsync(Stream input, CancellationToken token)
                => new ValueTask<T>((T)formatter.Deserialize(input));

            protected override ValueTask SerializeAsync(T input, Stream output, CancellationToken token)
            {
                formatter.Serialize(output, input);
                return new ValueTask();
            }
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

        private static Task Produce(ChannelWriter<decimal> writer) => Produce(writer, 0M, 500M);

        private static async Task Produce(ChannelWriter<decimal> writer, decimal start, decimal count)
        {
            for (decimal i = start; i < count; i++)
                await writer.WriteAsync(i);
        }

        private static async Task Consume(ChannelReader<decimal> reader)
        {
            for (decimal i = 0M; i < 500M; i++)
                Equal(i, await reader.ReadAsync());
        }

        private static async Task ConsumeInRange(ChannelReader<decimal> reader)
        {
            const decimal LowerBound = 0M;
            const decimal UpperBound = 500M;
            for (decimal i = LowerBound; i < UpperBound; i++)
                True((await reader.ReadAsync()).Between(LowerBound, UpperBound, BoundType.LeftClosed));
        }

        [Theory]
        [InlineData(0L)]
        [InlineData(102400L)]
        public static async Task ProduceConsumeConcurrently(long initialSize)
        {
            using var channel = new SerializationChannel<decimal>(new PersistentChannelOptions { SingleReader = true, SingleWriter = true, PartitionCapacity = 100, InitialPartitionSize = initialSize });
            var consumer = Consume(channel.Reader);
            var producer = Produce(channel.Writer);
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
    }
}
