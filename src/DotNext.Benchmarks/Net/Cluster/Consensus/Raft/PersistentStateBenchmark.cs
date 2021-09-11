using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO;
    using IO.Log;

    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [MemoryDiagnoser]
    public class PersistentStateBenchmark
    {
        private sealed class BinaryLogEntry : BinaryTransferObject, IRaftLogEntry
        {
            internal BinaryLogEntry(long term, ReadOnlyMemory<byte> content)
                : base(content)
            {
                Term = term;
                Timestamp = DateTimeOffset.UtcNow;
            }

            public long Term { get; }

            public DateTimeOffset Timestamp { get; }
        }

        private sealed class LogEntrySizeCounter : ILogEntryConsumer<IRaftLogEntry, long>
        {
            internal static readonly LogEntrySizeCounter Instance = new();

            private LogEntrySizeCounter()
            {

            }

            public async ValueTask<long> ReadAsync<TEntryImpl, TList>(TList entries, long? snapshotIndex, CancellationToken token)
                where TEntryImpl : notnull, IRaftLogEntry
                where TList : notnull, IReadOnlyList<TEntryImpl>
            {
                var result = 0L;
                foreach (var entry in entries)
                {
                    using var buffer = await entry.ToMemoryAsync();
                    result += buffer.Length;
                }

                return result;
            }
        }

        private readonly string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        private IPersistentState state;

        private async Task SetupStateAsync(PersistentState.Options options, bool addToCache)
        {
            var state = new PersistentState(path, 10, options);
            const int payloadSize = 2048;
            var bytes = new byte[payloadSize];
            Random.Shared.NextBytes(bytes);
            await state.AppendAsync(new BinaryLogEntry(10L, bytes), addToCache);
            Random.Shared.NextBytes(bytes);
            await state.AppendAsync(new BinaryLogEntry(20L, bytes), addToCache);
            this.state = state;
        }

        [GlobalSetup(Target = nameof(ReadPersistedLogEntriesAsync))]
        public Task SetupStateWithoutCacheAsync()
            => SetupStateAsync(new PersistentState.Options { UseCaching = false }, false);

        [GlobalSetup(Target = nameof(ReadCachedLogEntriesAsync))]
        public Task SetupStateWithCacheAsync()
            => SetupStateAsync(new PersistentState.Options { UseCaching = true }, true);

        [Benchmark]
        public ValueTask<long> ReadCachedLogEntriesAsync()
            => state.ReadAsync(LogEntrySizeCounter.Instance, 1, 2);

        [Benchmark]
        public ValueTask<long> ReadPersistedLogEntriesAsync()
            => state.ReadAsync(LogEntrySizeCounter.Instance, 1, 2);
    }
}