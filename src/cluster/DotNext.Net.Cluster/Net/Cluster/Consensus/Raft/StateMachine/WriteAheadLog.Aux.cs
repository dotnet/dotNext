using System.Collections;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;
using IO;
using IO.Log;

partial class WriteAheadLog
{
    private interface IConstant<out T>
    {
        static abstract T Value { get; }
    }

    private struct TrueConstant : IConstant<bool>
    {
        static bool IConstant<bool>.Value => true;
    }

    private struct FalseConstant : IConstant<bool>
    {
        static bool IConstant<bool>.Value => false;
    }

    [StructLayout(LayoutKind.Auto)]
    private struct BufferedLogEntry(MemoryOwner<byte> buffer) : IBufferedLogEntry, IDisposable
    {
        private MemoryOwner<byte> buffer = buffer;
        
        public readonly required DateTimeOffset Timestamp { get; init; }
        public readonly required long Term { get; init; }
        public readonly required object? Context { get; init; }
        public readonly required int? CommandId { get; init; }

        readonly long? IDataTransferObject.Length => buffer.Length;

        readonly bool IDataTransferObject.IsReusable => true;

        readonly bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
        {
            memory = buffer.Memory;
            return true;
        }

        bool ILogEntry.IsSnapshot => false;

        readonly ReadOnlySpan<byte> IBufferedLogEntry.Content => buffer.Span;

        readonly ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => writer.Invoke(buffer.Memory, token);
        
        public void Dispose() => buffer.Dispose();
    }
    
    [StructLayout(LayoutKind.Auto)]
    private readonly struct LogEntryList : IReadOnlyList<LogEntry>
    {
        private readonly long StartIndex;
        private readonly MetadataPageManager metadataPages;
        private readonly IMemoryView? dataPages; // if null, then take the metadata only
        private readonly ISnapshot? snapshot;

        internal LogEntryList(ISnapshotManager manager, long startIndex, long endIndex, IMemoryView? dataPages, MetadataPageManager metadataPages,
            out long? snapshotIndex)
        {
            snapshot = manager.Snapshot;

            if (snapshot?.Index is { } si && si >= startIndex)
            {
                startIndex = si;
                endIndex = long.Max(endIndex, si);
                snapshotIndex = si;
            }
            else
            {
                snapshotIndex = null;
                snapshot = null;
            }

            StartIndex = startIndex;
            var length = endIndex - startIndex + 1L;
            if (length > int.MaxValue)
                throw new InternalBufferOverflowException(ExceptionMessages.RangeTooBig);

            Count = (int)length;
            this.metadataPages = metadataPages;
            this.dataPages = dataPages;
        }

        public int Count { get; }

        public LogEntry this[int index]
        {
            get
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Count, nameof(index));

                return index is 0 && snapshot is not null
                    ? new(snapshot)
                    : Read(index);
            }
        }

        private LogEntry Read(int index)
        {
            var metadata = metadataPages[StartIndex + index];
            return new(metadata, index, dataPages);
        }

        public IEnumerator<LogEntry> GetEnumerator()
        {
            int index;
            if (snapshot is null)
            {
                index = 0;
            }
            else
            {
                index = 1;
                yield return new(snapshot);
            }

            while (index < Count)
                yield return Read(index++);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}