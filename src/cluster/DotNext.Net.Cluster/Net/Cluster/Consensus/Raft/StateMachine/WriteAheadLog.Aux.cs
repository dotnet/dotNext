using System.Collections;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;
using IO;
using IO.Log;

partial class WriteAheadLog
{
    /// <summary>
    /// Imports log entries from another WAL.
    /// </summary>
    /// <remarks>
    /// This method is intended for migration purposes only, it should not be used
    /// during the normal operation.
    /// </remarks>
    /// <param name="other">The source of log entries.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async Task ImportAsync(WriteAheadLog other, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(other);

        var reader = await other.ReadAsync(LastCommittedEntryIndex + 1L, other.LastEntryIndex, token).ConfigureAwait(false);
        try
        {
            foreach (var entry in reader)
            {
                await AppendAsync(entry, entry.Index, token).ConfigureAwait(false);
            }
        }
        finally
        {
            reader.Dispose();
        }

        await CommitAsync(other.LastCommittedEntryIndex, token).ConfigureAwait(false);
        await WaitForApplyAsync(other.LastAppliedIndex, token).ConfigureAwait(false);
        await FlushAsync(token).ConfigureAwait(false);
    }

    /// <summary>
    /// Represents catastrophic WAL failure.
    /// </summary>
    public abstract class IntegrityException : Exception
    {
        private protected IntegrityException(string message)
            : base(message)
        {
        }
    }
    
    /// <summary>
    /// Indicates that the hash of the log entry doesn't match.
    /// </summary>
    public sealed class HashMismatchException : IntegrityException
    {
        internal HashMismatchException()
            : base(ExceptionMessages.LogEntryHashMismatch)
        {
            
        }
    }
    
    /// <summary>
    /// Indicates that the log doesn't have a page on the disk.
    /// </summary>
    public sealed class MissingPageException : IntegrityException
    {
        internal MissingPageException(uint pageIndex)
            : base(ExceptionMessages.MissingWalPage(pageIndex))
        {
        }
    }
    
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
        
        public required long Term { get; init; }
        public required object? Context { get; init; }
        public required int? CommandId { get; init; }

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
            var absoluteIndex = StartIndex + index;
            var metadata = metadataPages.GetView<MetadataReader>(absoluteIndex).Metadata;
            return new(metadata, absoluteIndex, dataPages);
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