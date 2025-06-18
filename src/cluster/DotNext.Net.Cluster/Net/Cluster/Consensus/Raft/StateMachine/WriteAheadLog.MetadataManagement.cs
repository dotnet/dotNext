using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

partial class WriteAheadLog
{
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly MetadataPageManager metadataPages;

    private sealed class MetadataPageManager : PageManager
    {
        public new const int PageSize = Page.MinPageSize;

        public MetadataPageManager(DirectoryInfo location)
            : base(location, PageSize)
        {
            Debug.Assert(PageSize % LogEntryMetadata.AlignedSize is 0);
        }
        
        public ValueTask Flush(long fromIndex, long toIndex, CancellationToken token)
        {
            var startPage = GetStartPageIndex(fromIndex, out var startOffset);
            var endPage = GetEndPageIndex(toIndex, out var endOffset);
            return Flush(startPage, startOffset, endPage, endOffset, token);
        }

        private uint GetStartPageIndex(long index, out int offset)
            => GetPageIndex((ulong)index * LogEntryMetadata.AlignedSize, out offset);

        public uint GetEndPageIndex(long index, out int offset)
            => GetPageIndex((ulong)index * LogEntryMetadata.AlignedSize + LogEntryMetadata.AlignedSize, out offset);

        internal bool TryGetMetadata(long index, out LogEntryMetadata metadata)
        {
            var pageIndex = GetStartPageIndex(index, out var offset);
            if (TryGetValue(pageIndex, out var page))
            {
                metadata = new(page.GetSpan().Slice(offset));
                return true;
            }

            metadata = default;
            return false;
        }

        public LogEntryMetadata this[long index]
        {
            get
            {
                var pageIndex = GetStartPageIndex(index, out var offset);
                return TryGetValue(pageIndex, out var page)
                    ? new(page.GetSpan().Slice(offset))
                    : throw new ArgumentOutOfRangeException(nameof(index));
            }

            set
            {
                var page = GetOrAdd(GetStartPageIndex(index, out var offset));
                value.Format(page.GetSpan().Slice(offset));
            }
        }
    }
}