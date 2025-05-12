using System.Diagnostics;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

partial class PersistentState
{
    private readonly MetadataPageManager metadataPages;
    
    private sealed class MetadataPageManager : PageManager
    {
        internal MetadataPageManager(DirectoryInfo location, int pageSize)
            : base(location, pageSize)
        {
            Debug.Assert(pageSize % LogEntryMetadata.AlignedSize is 0);
        }

        public uint GetStartPageIndex(long index)
            => GetPageIndex((ulong)index * LogEntryMetadata.AlignedSize, out _);

        public uint GetEndPageIndex(long index)
            => GetPageIndex((ulong)index * LogEntryMetadata.AlignedSize + LogEntryMetadata.AlignedSize, out _);

        private Span<byte> GetMetadata(long index)
        {
            var pageIndex = GetPageIndex((ulong)index * LogEntryMetadata.AlignedSize, out var offset);

            if (TryGetValue(pageIndex, out var page))
                return page.GetSpan().Slice(offset);
            
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        public LogEntryMetadata this[long index]
        {
            get => new(GetMetadata(index));
            set => value.Format(GetMetadata(index));
        }
    }
}