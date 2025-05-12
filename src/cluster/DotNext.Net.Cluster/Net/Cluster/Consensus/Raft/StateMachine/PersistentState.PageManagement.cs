using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using DotNext.Buffers;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

partial class PersistentState
{
    private static uint GetPageIndex(ulong absoluteAddress, int pageSize, out int offset)
    {
        Debug.Assert(int.IsPow2(pageSize));

        offset = (int)(absoluteAddress & ((uint)pageSize - 1U));
        return (uint)(absoluteAddress >> BitOperations.TrailingZeroCount(pageSize));
    }
    
    private class PageManager : ConcurrentDictionary<uint, Page>, IDisposable
    {
        private readonly DirectoryInfo location;
        internal readonly int PageSize;
        
        internal PageManager(DirectoryInfo location, int pageSize)
        {
            Debug.Assert(int.IsPow2(pageSize));
            
            PageSize = pageSize;
            this.location = location;
            
            // populate pages
            foreach (var pageFile in location.EnumerateFiles())
            {
                if (uint.TryParse(pageFile.Name, provider: null, out var pageIndex))
                {
                    TryAdd(pageIndex, new(location, pageIndex, pageSize));
                }
            }
        }

        protected uint GetPageIndex(ulong absoluteAddress, out int offset)
            => PersistentState.GetPageIndex(absoluteAddress, PageSize, out offset);

        public void Delete(uint fromPage, uint toPage)
        {
            for (var pageIndex = fromPage; pageIndex < toPage; pageIndex++)
            {
                if (TryRemove(pageIndex, out var page))
                {
                    page.DisposeAndDelete();
                }
            }
        }

        public Page GetOrAdd(uint pageIndex)
        {
            Page? page;
            while (!TryGetValue(pageIndex, out page))
            {
                page = new Page(location, pageIndex, PageSize);

                if (TryAdd(pageIndex, page))
                    break;

                page.As<IDisposable>().Dispose();
            }

            return page;
        }

        public ReadOnlySequence<byte> Read(ulong address, long length)
        {
            var fromPageIndex = GetPageIndex(address, out var fromOffset);
            var toPageIndex = GetPageIndex((ulong)length + address, out var toOffset);

            // common case
            return fromPageIndex == toPageIndex
                ? new(this[fromPageIndex].Memory[fromOffset..toOffset])
                : ReadMultiSegment(fromPageIndex, fromOffset, toPageIndex, toOffset);
        }

        private ReadOnlySequence<byte> ReadMultiSegment(uint fromPageIndex, int fromOffset, uint toPageIndex, int toOffset)
        {
            var writer = new BufferWriterSlim<ReadOnlyMemory<byte>>((int)(toPageIndex - fromPageIndex));
            try
            {
                var segment = this[fromPageIndex].Memory.Slice(fromOffset);
                writer.Add(segment);

                for (var pageIndex = fromPageIndex + 1U; pageIndex < toPageIndex; pageIndex++)
                {
                    segment = this[pageIndex].Memory;
                    writer.Add(segment);
                }

                segment = this[toPageIndex].Memory.Slice(0, toOffset);
                writer.Add(segment);

                return Memory.ToReadOnlySequence(writer.WrittenSpan);
            }
            finally
            {
                writer.Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var page in Values)
            {
                page.As<IDisposable>().Dispose();
            }
        }
    }
}