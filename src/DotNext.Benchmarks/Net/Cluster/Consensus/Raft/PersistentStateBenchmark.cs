using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO;
using IO.Log;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class PersistentStateBenchmark
{
    private sealed class TestPersistentState : PersistentState
    {
        internal TestPersistentState(string path, Options configuration)
            : base(path, 10, configuration)
        {
        }

        protected override ValueTask ApplyAsync(LogEntry entry) => ValueTask.CompletedTask;

        protected override SnapshotBuilder CreateSnapshotBuilder(in SnapshotBuilderContext context)
            => throw new NotImplementedException();
    }

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
    private PersistentState state;
    private byte[] writePayload;

    private async Task PrepareForReadAsync(PersistentState.Options configuration, bool addToCache)
    {
        var state = new TestPersistentState(path, configuration);
        const int payloadSize = 2048;
        var bytes = new byte[payloadSize];
        Random.Shared.NextBytes(bytes);
        await state.AppendAsync(new BinaryLogEntry(10L, bytes), addToCache);
        Random.Shared.NextBytes(bytes);
        await state.AppendAsync(new BinaryLogEntry(20L, bytes), addToCache);
        this.state = state;
    }

    [GlobalCleanup]
    public void DisposeState() => state.Dispose();

    [GlobalSetup(Target = nameof(ReadPersistedLogEntriesAsync))]
    public Task PrepareForReadWithoutCacheAsync()
        => PrepareForReadAsync(new PersistentState.Options { UseCaching = false, CompactionMode = PersistentState.CompactionMode.Background }, false);

    [GlobalSetup(Target = nameof(ReadCachedLogEntriesAsync))]
    public Task PrepareForReadWithCacheAsync()
        => PrepareForReadAsync(new PersistentState.Options { UseCaching = true, CompactionMode = PersistentState.CompactionMode.Background }, true);

    [Benchmark]
    public ValueTask<long> ReadCachedLogEntriesAsync()
        => state.As<IPersistentState>().ReadAsync(LogEntrySizeCounter.Instance, 1, 2);

    [Benchmark]
    public ValueTask<long> ReadPersistedLogEntriesAsync()
        => state.As<IPersistentState>().ReadAsync(LogEntrySizeCounter.Instance, 1, 2);

    private void PrepareForWrite(PersistentState.Options configuration)
    {
        var state = new TestPersistentState(path, configuration);
        const int payloadSize = 2048;
        writePayload = new byte[payloadSize];
        Random.Shared.NextBytes(writePayload);
        this.state = state;
    }

    [GlobalSetup(Target = nameof(WriteUncachedLogEntryAsync))]
    public void PrepareForWriteWithoutCache()
        => PrepareForWrite(new PersistentState.Options { UseCaching = false, CompactionMode = PersistentState.CompactionMode.Background });

    [GlobalSetup(Target = nameof(WriteCachedLogEntryAsync))]
    public void PrepareForWriteWithCache()
        => PrepareForWrite(new PersistentState.Options { UseCaching = true, CompactionMode = PersistentState.CompactionMode.Background });

    [Benchmark]
    public async ValueTask WriteCachedLogEntryAsync()
    {
        await state.AppendAsync(new BinaryLogEntry(10L, writePayload));
        await state.DropAsync(1L, reuseSpace: true);
    }

    [Benchmark]
    public async ValueTask WriteUncachedLogEntryAsync()
    {
        await state.AppendAsync(new BinaryLogEntry(10L, writePayload));
        await state.DropAsync(1L, reuseSpace: true);
    }
}