using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

partial class WriteAheadLog
{
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly MetadataPageManager metadataPages;

    [StructLayout(LayoutKind.Auto)]
    private readonly struct MetadataPageManager(PageManager manager) : IDisposable
    {
        public const string LocationPrefix = "metadata";
        
        public long DeletePages(long toIndex)
        {
            var toPage = GetEndPageIndex(toIndex, out _);
            return manager.DeletePages(toPage) * manager.PageSize;
        }
        
        private uint GetStartPageIndex(long index, out int offset)
            => manager.GetPageIndex((ulong)index * LogEntryMetadata.AlignedSize, out offset);

        private uint GetEndPageIndex(long index, out int offset)
            => manager.GetPageIndex((ulong)index * LogEntryMetadata.AlignedSize + LogEntryMetadata.AlignedSize, out offset);

        public ValueTask FlushAsync(long fromIndex, long toIndex, CancellationToken token)
        {
            var startPage = GetStartPageIndex(fromIndex, out var startOffset);
            var endPage = GetEndPageIndex(toIndex, out var endOffset);
            return manager.FlushAsync(startPage, startOffset, endPage, endOffset, token);
        }
        
        public bool TryGetMetadata(long index, out LogEntryMetadata metadata)
        {
            var pageIndex = GetStartPageIndex(index, out var offset);
            if (manager.TryGetPage(pageIndex) is { } page)
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
                var page = manager[pageIndex];
                return new(page.GetSpan().Slice(offset));
            }

            set
            {
                var page = manager.GetOrAddPage(GetStartPageIndex(index, out var offset));
                value.Format(page.GetSpan().Slice(offset));
            }
        }

        public void Dispose() => manager.Dispose();
    }
}