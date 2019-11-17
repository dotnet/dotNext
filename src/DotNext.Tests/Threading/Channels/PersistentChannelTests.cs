using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading.Channels
{
    public sealed class PersistentChannelTests : Assert
    {
        private sealed class GuidChannel : PersistentChannel<Guid, Guid>
        {
            private readonly IFormatter formatter;

            internal GuidChannel(PersistentChannelOptions options)
                : base(options)
            {
                formatter = new BinaryFormatter();
            }

            protected override ValueTask<Guid> DeserializeAsync(Stream input, CancellationToken token)
                => new ValueTask<Guid>((Guid)formatter.Deserialize(input));

            protected override ValueTask SerializeAsync(Guid input, Stream output, CancellationToken token)
            {
                formatter.Serialize(output, input);
                return new ValueTask();
            }
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public static async Task ReadWrite(bool singleReader, bool singleWriter)
        {
            Guid g1 = Guid.NewGuid(), g2 = Guid.NewGuid(), g3 = Guid.NewGuid();
            using (var channel = new GuidChannel(new PersistentChannelOptions { SingleReader = singleReader, SingleWriter = singleWriter }))
            {
                False(channel.Writer.TryWrite(g1));
                await channel.Writer.WriteAsync(g1);
                await channel.Writer.WriteAsync(g2);
                await channel.Writer.WriteAsync(g3);
                Equal(g1, await channel.Reader.ReadAsync());
                Equal(g2, await channel.Reader.ReadAsync());
                True(await channel.Reader.WaitToReadAsync());
                True(channel.Reader.TryRead(out var last));
                Equal(g3, last);
                Equal(1D, channel.Ratio);
            }
        }

        [Fact]
        public static async Task Persistence()
        {
            Guid g1 = Guid.NewGuid(), g2 = Guid.NewGuid(), g3 = Guid.NewGuid();
            var options = new PersistentChannelOptions { BufferSize = 1024 };
            using (var channel = new GuidChannel(options))
            {
                await channel.Writer.WriteAsync(g1);
                await channel.Writer.WriteAsync(g2);
                await channel.Writer.WriteAsync(g3);
                Equal(g1, await channel.Reader.ReadAsync());
            }
            using (var channel = new GuidChannel(options))
            {
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
                RecordsPerPartition = 3
            };
            Guid g1 = Guid.NewGuid(), g2 = Guid.NewGuid(), g3 = Guid.NewGuid(), g4 = Guid.NewGuid();
            using (var channel = new GuidChannel(options))
            {
                await channel.Writer.WriteAsync(g1);
                await channel.Writer.WriteAsync(g2);
                await channel.Writer.WriteAsync(g3);
                await channel.Writer.WriteAsync(g4);
                Equal(g1, await channel.Reader.ReadAsync());
                Equal(g2, await channel.Reader.ReadAsync());
                Equal(g3, await channel.Reader.ReadAsync());
                Equal(g4, await channel.Reader.ReadAsync());
            }
        }

        [Fact]
        public static async Task PersistentPartitionOverflow()
        {
            var options = new PersistentChannelOptions
            {
                SingleReader = true,
                SingleWriter = true,
                RecordsPerPartition = 3
            };
            Guid g1 = Guid.NewGuid(), g2 = Guid.NewGuid(), g3 = Guid.NewGuid(), g4 = Guid.NewGuid();
            using (var channel = new GuidChannel(options))
            {
                await channel.Writer.WriteAsync(g1);
                await channel.Writer.WriteAsync(g2);
                await channel.Writer.WriteAsync(g3);
                await channel.Writer.WriteAsync(g4);
                Equal(g1, await channel.Reader.ReadAsync());
            }
            using (var channel = new GuidChannel(options))
            {
                Equal(g2, await channel.Reader.ReadAsync());
                Equal(g3, await channel.Reader.ReadAsync());
                Equal(g4, await channel.Reader.ReadAsync());
            }
        }
    }
}
