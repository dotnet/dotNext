namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using IO;
    using IO.Log;

    public sealed class BufferedRaftLogEntryListTests : Test
    {
        private readonly struct RaftLogEntry : IRaftLogEntry
        {
            private readonly ReadOnlyMemory<byte> content;
            private readonly bool knownLength;

            internal RaftLogEntry(long term, byte[] content, bool knownLength)
            {
                Term = term;
                Timestamp = DateTimeOffset.UtcNow;
                this.content = content;
                this.knownLength = knownLength;
            }

            public long Term { get; }

            public DateTimeOffset Timestamp { get; }

            bool IDataTransferObject.IsReusable => true;

            long? IDataTransferObject.Length => knownLength ? content.Length : null;

            ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
                => writer.WriteAsync(content, null, token);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task BufferizeLogEntries(bool knownLength)
        {
            var content1 = RandomBytes(1024);
            var content2 = RandomBytes(4096);

            await using var entries = new LogEntryProducer<RaftLogEntry>(new RaftLogEntry(42L, content1, knownLength), new RaftLogEntry(43L, content2, knownLength));
            var options = new RaftLogEntriesBufferingOptions { MemoryThreshold = 1025 };
            using var buffered = await BufferedRaftLogEntryList.CopyAsync(entries, options);
            Equal(2L, buffered.Count);
            Equal(content1, await buffered[0].ToByteArrayAsync());
            Equal(content2, await buffered[1].ToByteArrayAsync());
        }
    }
}