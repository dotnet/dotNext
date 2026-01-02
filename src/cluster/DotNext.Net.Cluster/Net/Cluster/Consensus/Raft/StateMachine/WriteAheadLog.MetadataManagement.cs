using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

partial class WriteAheadLog
{
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly MetadataPageManager metadataPages;

    [StructLayout(LayoutKind.Auto)]
    private readonly struct MetadataPageManager(PageManager manager, int hashSizeInBytes) : IDisposable
    {
        public const string LocationPrefix = "metadata";

        private readonly int MetadataEntryAlignedSize = GetAlignedSize(LogEntryMetadata.Size + hashSizeInBytes, manager.PageSize);
        
        private static int GetAlignedSize(int headerSize, int containerSize)
        {
            var best = int.MaxValue;

            for (var i = 1; i * i <= containerSize; i++)
            {
                if (containerSize % i is not 0)
                    continue;

                var d2 = containerSize / i;

                if (i >= headerSize && i < best)
                    best = i;

                if (d2 >= headerSize && d2 < best)
                    best = d2;
            }

            return best is int.MaxValue
                ? throw new OverflowException()
                : best;
        }
        
        public long DeletePages(long toIndex)
        {
            var toPage = GetEndPageIndex(toIndex, out _);
            return manager.DeletePages(toPage) * manager.PageSize;
        }
        
        private uint GetStartPageIndex(long index, out int offset)
            => manager.GetPageIndex((ulong)index * (uint)MetadataEntryAlignedSize, out offset);

        private uint GetEndPageIndex(long index, out int offset)
            => manager.GetPageIndex((ulong)index * (uint)MetadataEntryAlignedSize + (uint)MetadataEntryAlignedSize, out offset);

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

        public TView GetView<TView>(long index)
            where TView : IMetadataView<TView>, allows ref struct
        {
            var pageIndex = GetStartPageIndex(index, out var offset);
            var page = TView.GetPage(manager, pageIndex);
            return TView.Create(page.GetSpan().Slice(offset, MetadataEntryAlignedSize));
        }

        public void Dispose() => manager.Dispose();
    }
    
    private interface IMetadataView<out TView>
        where TView : IMetadataView<TView>, allows ref struct
    {
        public static abstract MemoryManager<byte> GetPage(PageManager manager, uint pageIndex);

        public static abstract TView Create(Span<byte> buffer);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct MetadataReader : IMetadataView<MetadataReader>
    {
        private readonly Span<byte> buffer;

        private MetadataReader(Span<byte> buffer) => this.buffer = buffer;

        public LogEntryMetadata Metadata => new(buffer);

        [SkipLocalsInit]
        public void CompleteAndVerifyHash(NonCryptographicHashAlgorithm hash)
        {
            ReadOnlySpan<byte> metadata = buffer.TrimLength(LogEntryMetadata.Size, out var expectedHash);
            hash.Append(metadata);

            Span<byte> actualHash = stackalloc byte[8]; // all our hashes no longer than 8 bytes
            var hashSize = hash.GetHashAndReset(actualHash);

            expectedHash = expectedHash.Slice(0, hashSize);
            actualHash = actualHash.Slice(0, hashSize);

            if (!expectedHash.SequenceEqual(actualHash))
                throw new HashMismatchException();
        }

        static MemoryManager<byte> IMetadataView<MetadataReader>.GetPage(PageManager manager, uint pageIndex)
            => manager[pageIndex];

        static MetadataReader IMetadataView<MetadataReader>.Create(Span<byte> buffer)
            => new(buffer);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct MetadataWriter : IMetadataView<MetadataWriter>
    {
        private readonly Span<byte> buffer;

        private MetadataWriter(Span<byte> buffer) => this.buffer = buffer;

        public void WriteMetadata(LogEntryMetadata metadata)
            => metadata.Format(buffer);

        public void CompleteAndWriteHash(NonCryptographicHashAlgorithm hash)
        {
            ReadOnlySpan<byte> metadata = buffer.TrimLength(LogEntryMetadata.Size, out var hashBuf);
            hash.Append(metadata);
            hash.GetHashAndReset(hashBuf);
        }

        static MemoryManager<byte> IMetadataView<MetadataWriter>.GetPage(PageManager manager, uint pageIndex)
            => manager.GetOrAddPage(pageIndex);

        static MetadataWriter IMetadataView<MetadataWriter>.Create(Span<byte> buffer)
            => new(buffer);
    }
}