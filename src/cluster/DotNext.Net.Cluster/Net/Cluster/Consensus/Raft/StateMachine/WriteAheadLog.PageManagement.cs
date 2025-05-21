using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;
using Collections.Generic;

partial class WriteAheadLog
{
    private static int GetPageOffset(ulong address, int pageSize)
    {
        Debug.Assert(int.IsPow2(pageSize));
        return (int)(address & ((uint)pageSize - 1U));
    }
    
    private static uint GetPageIndex(ulong address, int pageSize, out int offset)
    {
        Debug.Assert(int.IsPow2(pageSize));

        offset = GetPageOffset(address, pageSize);
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

        public MemoryRange GetRange(ulong offset, long length) => new(this, offset, length);

        public void Dispose()
        {
            foreach (var page in Values)
            {
                page.As<IDisposable>().Dispose();
            }
        }
        
        [StructLayout(LayoutKind.Auto)]
        public readonly struct MemoryRange(PageManager manager, ulong offset, long length) : IEnumerable<ReadOnlyMemory<byte>>
        {
            public Enumerator GetEnumerator() => new(manager, offset, length);

            private IEnumerator<ReadOnlyMemory<byte>> ToClassicEnumerator()
                => GetEnumerator().ToClassicEnumerator<Enumerator, ReadOnlyMemory<byte>>();
            
            IEnumerator<ReadOnlyMemory<byte>> IEnumerable<ReadOnlyMemory<byte>>.GetEnumerator()
                => ToClassicEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => ToClassicEnumerator();

            public bool TryGetMemory(out ReadOnlyMemory<byte> memory)
            {
                var enumerator = GetEnumerator();
                var result = !enumerator.MoveNext() || !enumerator.HasNext;
                memory = enumerator.Current;
                return result;
            }

            public ReadOnlySequence<byte> ToReadOnlySequence()
            {
                var enumerator = GetEnumerator();
                return !enumerator.MoveNext() || !enumerator.HasNext
                    ? new(enumerator.Current)
                    : ReadMultiSegment(ref enumerator);

                static ReadOnlySequence<byte> ReadMultiSegment(ref Enumerator enumerator)
                {
                    var buffer = new ReadOnlyMemoryArray();
                    var writer = new BufferWriterSlim<ReadOnlyMemory<byte>>(buffer);
                    writer.Add() = enumerator.Current;

                    while (enumerator.MoveNext())
                    {
                        writer.Add() = enumerator.Current;
                    }

                    return Memory.ToReadOnlySequence(writer.WrittenSpan);
                }
            }

            public static implicit operator ReadOnlySequence<byte>(in MemoryRange range) => range.ToReadOnlySequence();
            
            [StructLayout(LayoutKind.Auto)]
            public struct Enumerator : IEnumerator<Enumerator, ReadOnlyMemory<byte>>
            {
                private readonly ConcurrentDictionary<uint, Page> pages;
                private long length;
                private uint pageIndex;
                private int offset;
                private Memory<byte> current;

                public Enumerator(PageManager manager, ulong address, long length)
                {
                    pages = manager;
                    pageIndex = manager.GetPageIndex(address, out offset);
                    this.length = length;
                }

                [UnscopedRef]
                public readonly ref readonly Memory<byte> Current => ref current;

                ReadOnlyMemory<byte> IEnumerator<Enumerator, ReadOnlyMemory<byte>>.Current => Current;

                public readonly bool HasNext => length > 0L;

                public bool MoveNext()
                {
                    if (HasNext)
                    {
                        var page = pages[pageIndex++];
                        current = page.Memory.Slice(offset).TrimLength(int.CreateSaturating(length));
                        length -= current.Length;
                        offset = 0;
                        return true;
                    }

                    return false;
                }
            }
        }

        [InlineArray(3)]
        private struct ReadOnlyMemoryArray
        {
            private ReadOnlyMemory<byte> element0;
        }
    }
}