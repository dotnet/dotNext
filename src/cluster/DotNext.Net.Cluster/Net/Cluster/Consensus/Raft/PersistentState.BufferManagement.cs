using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO.Log;
using static IO.DataTransferObject;

public partial class PersistentState
{
    internal sealed class BufferingLogEntryConsumer : ConcurrentBag<BufferedLogEntry[]>, ILogEntryConsumer<IRaftLogEntry, (BufferedLogEntryList, long?)>
    {
        private readonly LogEntriesBufferingOptions options;

        internal BufferingLogEntryConsumer(LogEntriesBufferingOptions options)
            => this.options = options;

        ValueTask<(BufferedLogEntryList, long?)> ILogEntryConsumer<IRaftLogEntry, (BufferedLogEntryList, long?)>.ReadAsync<TEntry, TList>(TList list, long? snapshotIndex, CancellationToken token)
        {
            var count = list.Count;

            // make the array available to GC if it has inappropriate length
            if (!TryTake(out var array) || array.Length < count)
                array = new BufferedLogEntry[count];

            return CopyAsync(BufferedLogEntryList.BufferizeAsync<TEntry, TList>(list, options, token), new(array, 0, count), snapshotIndex);

            static async ValueTask<(BufferedLogEntryList, long?)> CopyAsync(IAsyncEnumerator<BufferedLogEntry> source, ArraySegment<BufferedLogEntry> destination, long? snapshotIndex)
            {
                try
                {
                    for (int i = 0; await source.MoveNextAsync().ConfigureAwait(false); i++)
                    {
                        destination[i] = source.Current;
                    }
                }
                finally
                {
                    await source.DisposeAsync().ConfigureAwait(false);
                }

                return new(new BufferedLogEntryList(destination), snapshotIndex);
            }
        }
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
            var error = default(Exception);
            try
            {
                while (await entries.MoveNextAsync().ConfigureAwait(false))
                {
                    var current = entries.Current;
                    var cachedEntry = new CachedLogEntry
                    {
                        Content = await current.ToMemoryAsync(allocator, Token).ConfigureAwait(false),
                        Term = current.Term,
                        CommandId = current.CommandId,
                        Timestamp = current.Timestamp,
                        PersistenceMode = CachedLogEntryPersistenceMode.SkipBuffer,
                    };

                    await queue.Writer.WriteAsync(cachedEntry, Token).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            queue.Writer.Complete(error);
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
    internal readonly struct BufferManager(IBufferManagerSettings options)
    {
        private readonly MemoryAllocator<CacheRecord>? cacheAllocator = options.UseCaching ? options.GetMemoryAllocator<CacheRecord>() : null;

        internal bool IsCachingEnabled => cacheAllocator is not null;

        internal MemoryAllocator<byte> BufferAllocator { get; } = options.GetMemoryAllocator<byte>();

        internal MemoryOwner<CacheRecord> AllocLogEntryCache(int recordsPerPartition)
            => cacheAllocator is null ? default : cacheAllocator(recordsPerPartition);

        internal MemoryOwner<T> Allocate<T>(int length) where T : unmanaged => options.GetMemoryAllocator<T>().AllocateExactly(length);
    }

    private readonly BufferingLogEntryConsumer? bufferingConsumer;
}