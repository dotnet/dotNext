using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using DotNext.Buffers;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

partial class WriteAheadLog
{
    private static uint GetPageIndex(ulong address, int pageSize, out int offset)
    {
        Debug.Assert(int.IsPow2(pageSize));

        offset = (int)(address & ((uint)pageSize - 1U));
        return (uint)(address >> BitOperations.TrailingZeroCount(pageSize));
    }
    
    private class PageManager : ConcurrentDictionary<uint, Page>, IDisposable
    {
        private readonly DirectoryInfo location;
        internal readonly int PageSize;

        protected PageManager(DirectoryInfo location, int pageSize)
            : base(DictionaryConcurrencyLevel, GetPageFiles(location, pageSize, out var pages))
        {
            Debug.Assert(int.IsPow2(pageSize));

            PageSize = pageSize;
            this.location = location;

            // populate pages
            if (pages.IsEmpty)
            {
                TryAdd(0U, new Page(location, 0U, pageSize));
            }
            else
            {
                foreach (var pageFile in pages)
                {
                    TryAdd(pageFile.Key, pageFile.Value);
                }
            }
        }

        private static int GetPageFiles(DirectoryInfo location, int pageSize, out ReadOnlySpan<KeyValuePair<uint, Page>> pageFiles)
        {
            pageFiles = GetPages(location)
                .Select<uint, KeyValuePair<uint, Page>>(pageIndex =>
                    new(pageIndex, new Page(location, pageIndex, pageSize)))
                .ToArray();

            return Math.Min(pageFiles.Length, 11);
        }

        private static IEnumerable<uint> GetPages(DirectoryInfo location)
        {
            return location.EnumerateFiles()
                .Select(static info => uint.TryParse(info.Name, provider: null, out var pageIndex) ? new uint?(pageIndex) : null)
                .Where(static pageIndex => pageIndex.HasValue)
                .Select(static pageIndex => pageIndex.GetValueOrDefault());
        }

        public uint GetPageIndex(ulong address, out int offset)
            => WriteAheadLog.GetPageIndex(address, PageSize, out offset);

        public int Delete(uint toPage) // exclusively
        {
            var count = 0;

            foreach (var pageIndex in GetPages(location))
            {
                if (pageIndex < toPage && TryRemove(pageIndex, out var page))
                {
                    page.DisposeAndDelete();
                    count++;
                }
            }

            return count;
        }

        protected Page GetOrAdd(uint pageIndex)
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
        
        protected Page GetOrAdd(ulong address, out int offset)
            => GetOrAdd(GetPageIndex(address, out offset));

        public ReadOnlySequence<byte> Read(ulong address, long length)
        {
            var pageIndex = GetPageIndex(address, out var offset);
            var page = this[pageIndex];
            ReadOnlyMemory<byte> block = page.Memory.Slice(offset).TrimLength(int.CreateSaturating(length));
            return block.Length == length ? new(block) : ReadMultisegment(pageIndex, ref block, length);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private ReadOnlySequence<byte> ReadMultisegment(uint pageIndex, ref ReadOnlyMemory<byte> block, long length)
        {
            var buffer = new ReadOnlyMemoryArray();
            var writer = new BufferWriterSlim<ReadOnlyMemory<byte>>(buffer);
            try
            {
                writer.Add() = block;
                length -= block.Length;
                
                do
                {
                    var page = this[++pageIndex];
                    block = page.Memory.TrimLength(int.CreateSaturating(length));
                    length -= block.Length;
                    writer.Add() = block;
                } while (length > 0L);

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

        [InlineArray(3)]
        private struct ReadOnlyMemoryArray
        {
            private ReadOnlyMemory<byte> element0;
        }
    }
}