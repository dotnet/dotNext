using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;

partial class WriteAheadLog
{
    /// <summary>
    /// Represents memory-mapped page of memory.
    /// </summary>
    private sealed unsafe class Page : MemoryManager<byte>
    {
        public const int MinPageSize = 4096;
        private readonly int pageSize, poolIndex;
        private void* address;

        public Page(int pageSize)
        {
            Debug.Assert(pageSize % MinPageSize is 0);

            address = NativeMemory.AlignedAlloc((uint)pageSize, (uint)Environment.SystemPageSize);
            
            this.pageSize = pageSize;
            poolIndex = -1;
        }

        public void Clear() => NativeMemory.Clear(address, (uint)pageSize);

        public void Discard()
        {
            if ((pageSize & (Environment.SystemPageSize - 1)) is 0)
            {
                UnmanagedMemory.Discard(GetSpan());
            }
        }

        public int PoolIndex
        {
            get => poolIndex;
            init => poolIndex = value;
        }

        public void Populate(DirectoryInfo location, uint pageIndex)
        {
            using var handle = File.OpenHandle(GetPageFileName(location, pageIndex), options: FileOptions.SequentialScan);
            RandomAccess.Read(handle, GetSpan(), fileOffset: 0L);
        }
        
        static string GetPageFileName(DirectoryInfo directory, uint pageIndex)
            => Path.Combine(directory.FullName, pageIndex.ToString(InvariantCulture));

        public static void Delete(DirectoryInfo directory, uint pageIndex)
            => File.Delete(GetPageFileName(directory, pageIndex));

        private void Flush(DirectoryInfo directory, uint pageIndex, int offset, int length)
        {
            using var handle = File.OpenHandle(GetPageFileName(directory, pageIndex), FileMode.OpenOrCreate, FileAccess.Write,
                options: FileOptions.WriteThrough);

            if (RandomAccess.GetLength(handle) is 0U)
                RandomAccess.SetLength(handle, pageSize);

            var buffer = GetSpan().Slice(offset, length);
            RandomAccess.Write(handle, buffer, offset);
        }

        public void Flush(DirectoryInfo directory, uint pageIndex, Range range)
        {
            var (offset, length) = range.GetOffsetAndLength(pageSize);
            Flush(directory, pageIndex, offset, length);
        }

        public override Span<byte> GetSpan() => new(address, pageSize);

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)elementIndex, (uint)pageSize, nameof(elementIndex));

            var pointer = (nuint)address + (uint)elementIndex;
            return new(pointer.ToPointer());
        }

        public override void Unpin()
        {
            // nothing to do
        }

        public override Memory<byte> Memory => CreateMemory(pageSize);

        protected override void Dispose(bool disposing)
        {
            if (address is not null)
            {
                NativeMemory.AlignedFree(address);
                address = null;
            }
        }
    }
}