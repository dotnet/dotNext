using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;
    using IO.Log;

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

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct BufferManager
        {
            private readonly MemoryAllocator<IMemoryOwner<byte>?>? cacheAllocator;
            private readonly MemoryAllocator<LogEntry> entryAllocator;

            internal BufferManager(IBufferManagerSettings options)
            {
                cacheAllocator = options.UseCaching ? options.GetMemoryAllocator<IMemoryOwner<byte>?>() : null;
                BufferAllocator = options.GetMemoryAllocator<byte>();
                entryAllocator = options.GetMemoryAllocator<LogEntry>();
            }

            internal bool IsCachingEnabled => cacheAllocator is not null;

            internal MemoryAllocator<byte> BufferAllocator { get; }

            internal PooledBufferWriter<byte> CreateBufferWriter(long length)
                => length <= int.MaxValue ? new(BufferAllocator, (int)length) : throw new InsufficientMemoryException();

            internal MemoryOwner<IMemoryOwner<byte>?> AllocLogEntryCache(int recordsPerPartition)
                => cacheAllocator is null ? default : cacheAllocator.Invoke(recordsPerPartition, true);

            internal MemoryOwner<LogEntry> AllocLogEntryList(int length) => entryAllocator.Invoke(length, true);
        }

        private readonly ILogEntryConsumer<IRaftLogEntry, (BufferedRaftLogEntryList, long?)>? bufferingConsumer;
    }
}