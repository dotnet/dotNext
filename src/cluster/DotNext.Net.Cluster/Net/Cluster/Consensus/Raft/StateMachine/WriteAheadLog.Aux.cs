using System.Collections;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

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
    private readonly struct LogEntryList : IReadOnlyList<LogEntry>
    {
        private readonly long StartIndex;
        private readonly int count;
        private readonly MetadataPageManager metadataPages;
        private readonly PageManager? dataPages; // if null, then take the metadata only
        private readonly ISnapshot? snapshot;

        internal LogEntryList(ISnapshotManager manager, long startIndex, long endIndex, PageManager? dataPages, MetadataPageManager metadataPages,
            out long? snapshotIndex)
        {
            snapshot = manager.TakeSnapshot();

            if (snapshot is not null)
            {
                startIndex = long.Max(startIndex, snapshot.Index);
                endIndex = long.Max(endIndex, startIndex);
                snapshotIndex = snapshot.Index;
            }
            else
            {
                snapshotIndex = null;
            }

            StartIndex = startIndex;
            var length = endIndex - startIndex + 1L;
            if (length > int.MaxValue)
                throw new InternalBufferOverflowException(ExceptionMessages.RangeTooBig);

            count = (int)length;
            this.metadataPages = metadataPages;
            this.dataPages = dataPages;
        }

        int IReadOnlyCollection<LogEntry>.Count => count;

        LogEntry IReadOnlyList<LogEntry>.this[int index]
        {
            get
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)count, nameof(index));

                return index is 0 && snapshot is not null
                    ? new(snapshot)
                    : Read(index);
            }
        }

        private LogEntry Read(int index)
        {
            var absoluteIndex = StartIndex + index;
            return dataPages is not null
                ? new(metadataPages.Read(absoluteIndex, dataPages, out var metadata), in metadata, absoluteIndex)
                : new(metadataPages[absoluteIndex], absoluteIndex);
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

            while (index < count)
                yield return Read(index++);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}