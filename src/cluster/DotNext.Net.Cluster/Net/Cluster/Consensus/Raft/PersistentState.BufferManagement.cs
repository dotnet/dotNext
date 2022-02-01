using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO.Log;
using static IO.DataTransferObject;

public partial class PersistentState
{
    private sealed class BufferingLogEntryConsumer : ILogEntryConsumer<IRaftLogEntry, (BufferedRaftLogEntryList, long?)>
    {
        private readonly RaftLogEntriesBufferingOptions options;

        internal BufferingLogEntryConsumer(RaftLogEntriesBufferingOptions options)
            => this.options = options;

        public async ValueTask<(BufferedRaftLogEntryList, long?)> ReadAsync<TEntry, TList>(TList entries, long? snapshotIndex, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
            where TList : notnull, IReadOnlyList<TEntry>
            => (await BufferedRaftLogEntryList.CopyAsync<TEntry, TList>(entries, options, token).ConfigureAwait(false), snapshotIndex);
    }

    private sealed class BufferingLogEntryProducer<TEntry> : ILogEntryProducer<CachedLogEntry>
        where TEntry : notnull, IRaftLogEntry
    {
        private readonly Channel<CachedLogEntry> queue;
        private readonly MemoryAllocator<byte> allocator;
        private readonly ILogEntryProducer<TEntry> entries;
        private CachedLogEntry current;
        private long count;

        internal BufferingLogEntryProducer(ILogEntryProducer<TEntry> entries, MemoryAllocator<byte> allocator)
        {
            this.entries = entries;
            count = entries.RemainingCount;
            queue = count < int.MaxValue
                ? Channel.CreateBounded<CachedLogEntry>(ConfigureOptions<BoundedChannelOptions>(new((int)count) { FullMode = BoundedChannelFullMode.Wait }))
                : Channel.CreateUnbounded<CachedLogEntry>(ConfigureOptions<UnboundedChannelOptions>(new()));
            this.allocator = allocator;
        }

        private static TOptions ConfigureOptions<TOptions>(TOptions options)
            where TOptions : ChannelOptions
        {
            options.AllowSynchronousContinuations = false;
            options.SingleReader = true;
            options.SingleWriter = true;
            return options;
        }

        internal CancellationToken Token { private get; init; }

        internal async Task BufferizeAsync()
        {
            try
            {
                while (await entries.MoveNextAsync().ConfigureAwait(false))
                {
                    var current = entries.Current;
                    var cachedEntry = new CachedLogEntry
                    {
                        Content = await BufferizeAsync(current, out var completedSynchronously, allocator, Token).ConfigureAwait(false),
                        Term = current.Term,
                        CommandId = current.CommandId,
                        Timestamp = current.Timestamp,
                        PersistenceMode = completedSynchronously ? CachedLogEntryPersistenceMode.CopyToBuffer : CachedLogEntryPersistenceMode.WriteThrough,
                    };

                    await queue.Writer.WriteAsync(cachedEntry, Token).ConfigureAwait(false);
                }

                queue.Writer.Complete();
            }
            catch (Exception e)
            {
                queue.Writer.Complete(e);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static ValueTask<MemoryOwner<byte>> BufferizeAsync(TEntry entry, out bool completedSynchronously, MemoryAllocator<byte> allocator, CancellationToken token)
            {
                var task = entry.ToMemoryAsync(allocator, token);
                completedSynchronously = task.IsCompleted;
                return task;
            }
        }

        long ILogEntryProducer<CachedLogEntry>.RemainingCount => count;

        private async ValueTask<bool> MoveNextAsync()
        {
            if (await queue.Reader.WaitToReadAsync(Token).ConfigureAwait(false) && queue.Reader.TryRead(out current))
            {
                count--;
                return true;
            }

            return false;
        }

        ValueTask<bool> IAsyncEnumerator<CachedLogEntry>.MoveNextAsync()
        {
            ValueTask<bool> result;
            if (queue.Reader.TryRead(out current))
            {
                count--;
                result = new(true);
            }
            else if (count <= 0L)
            {
                result = new(false);
            }
            else
            {
                result = MoveNextAsync();
            }

            return result;
        }

        CachedLogEntry IAsyncEnumerator<CachedLogEntry>.Current => current;

        ValueTask IAsyncDisposable.DisposeAsync() => ValueTask.CompletedTask;
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct BufferManager
    {
        private readonly MemoryAllocator<CacheRecord>? cacheAllocator;
        private readonly MemoryAllocator<LogEntry> entryAllocator;

        internal BufferManager(IBufferManagerSettings options)
        {
            cacheAllocator = options.UseCaching ? options.GetMemoryAllocator<CacheRecord>() : null;
            BufferAllocator = options.GetMemoryAllocator<byte>();
            entryAllocator = options.GetMemoryAllocator<LogEntry>();
        }

        internal bool IsCachingEnabled => cacheAllocator is not null;

        internal MemoryAllocator<byte> BufferAllocator { get; }

        internal MemoryOwner<CacheRecord> AllocLogEntryCache(int recordsPerPartition)
            => cacheAllocator is null ? default : cacheAllocator(recordsPerPartition);

        internal MemoryOwner<LogEntry> AllocLogEntryList(int length) => entryAllocator(length);
    }

    private readonly ILogEntryConsumer<IRaftLogEntry, (BufferedRaftLogEntryList, long?)>? bufferingConsumer;
}