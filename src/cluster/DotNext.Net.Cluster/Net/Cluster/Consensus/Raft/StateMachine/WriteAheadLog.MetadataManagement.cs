using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

partial class WriteAheadLog
{
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly MetadataPageManager metadataPages;

    private sealed class MetadataPageManager : PageManager
    {
        private new const int PageSize = 4096 * 2;

        public MetadataPageManager(DirectoryInfo location)
            : base(location, PageSize)
        {
            Debug.Assert(PageSize % LogEntryMetadata.AlignedSize is 0);
        }

        public long? GetLowestIndex()
        {
            using var enumerator = GetPages().GetEnumerator();

            if (!enumerator.MoveNext())
                return null;

            var lowestPage = enumerator.Current;
            do
            {
                lowestPage = Math.Min(lowestPage, enumerator.Current);
            } while (enumerator.MoveNext());

            var entriesPerPage = PageSize / LogEntryMetadata.AlignedSize;
            return (uint)entriesPerPage * (long)lowestPage;
        }

        public uint GetStartPageIndex(long index)
            => GetPageIndex((ulong)index * LogEntryMetadata.AlignedSize, out _);

        public uint GetEndPageIndex(long index)
            => GetPageIndex((ulong)index * LogEntryMetadata.AlignedSize + LogEntryMetadata.AlignedSize, out _);

        private Span<byte> TryGetMetadata(long index)
        {
            var pageIndex = GetPageIndex((ulong)index * LogEntryMetadata.AlignedSize, out var offset);

            return TryGetValue(pageIndex, out var page)
                ? page.GetSpan().Slice(offset)
                : Span<byte>.Empty;
        }

        internal bool TryGetMetadata(long index, out LogEntryMetadata metadata)
        {
            if (TryGetMetadata(index) is { Length: > 0 } metadataSpan)
            {
                metadata = new(metadataSpan);
                return true;
            }

            metadata = default;
            return false;
        }

        public LogEntryMetadata this[long index]
        {
            get => TryGetMetadata(index) is { Length: > 0 } metadata
                ? new(metadata)
                : throw new ArgumentOutOfRangeException(nameof(index));

            set
            {
                if (TryGetMetadata(index) is not { Length: > 0 } metadata)
                    throw new ArgumentOutOfRangeException(nameof(index));

                value.Format(metadata);
            }
        }

        public ReadOnlySequence<byte> Read(long index, PageManager dataPages, out LogEntryMetadata metadata)
        {
            metadata = this[index];
            return dataPages.Read(metadata.Offset, metadata.Length);
        }
    }
}