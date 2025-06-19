using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;

partial class WriteAheadLog
{
    private const int MinPageSize = 4096;
    
    /// <summary>
    /// Represents memory-mapped page of memory.
    /// </summary>
    private sealed class AnonymousPage : MemoryManager<byte>
    {
        private readonly int pageSize;
        private unsafe void* address;

        public unsafe AnonymousPage(int pageSize)
        {
            Debug.Assert(pageSize % MinPageSize is 0);

            address = NativeMemory.AlignedAlloc((uint)pageSize, (uint)Environment.SystemPageSize);
            
            this.pageSize = pageSize;
            PoolIndex = -1;
        }

        public unsafe void Clear() => NativeMemory.Clear(address, (uint)pageSize);

        public void Discard()
        {
            if ((pageSize & (Environment.SystemPageSize - 1)) is 0)
            {
                UnmanagedMemory.Discard(GetSpan());
            }
        }

        public int PoolIndex { get; init; }

        public void Populate(DirectoryInfo location, uint pageIndex)
        {
            using var handle = File.OpenHandle(GetPageFileName(location, pageIndex), options: FileOptions.SequentialScan);
            RandomAccess.Read(handle, GetSpan(), fileOffset: 0L);
        }
        
        static string GetPageFileName(DirectoryInfo directory, uint pageIndex)
            => Path.Combine(directory.FullName, pageIndex.ToString(InvariantCulture));

        public static void Delete(DirectoryInfo directory, uint pageIndex)
            => File.Delete(GetPageFileName(directory, pageIndex));

        private async ValueTask Flush(DirectoryInfo directory, uint pageIndex, int offset, int length, CancellationToken token)
        {
            using var handle = File.OpenHandle(GetPageFileName(directory, pageIndex),
                FileMode.OpenOrCreate,
                FileAccess.Write,
                options: FileOptions.WriteThrough | FileOptions.Asynchronous);

            if (RandomAccess.GetLength(handle) is 0U)
                RandomAccess.SetLength(handle, pageSize);

            var buffer = Memory.Slice(offset, length);
            await RandomAccess.WriteAsync(handle, buffer, offset, token).ConfigureAwait(false);
        }

        public ValueTask Flush(DirectoryInfo directory, uint pageIndex, Range range, CancellationToken token)
        {
            var (offset, length) = range.GetOffsetAndLength(pageSize);
            return Flush(directory, pageIndex, offset, length, token);
        }

        public override unsafe Span<byte> GetSpan() => new(address, pageSize);

        public override unsafe MemoryHandle Pin(int elementIndex = 0)
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

        protected override unsafe void Dispose(bool disposing)
        {
            if (address is not null)
            {
                NativeMemory.AlignedFree(address);
                address = null;
            }
        }

        [SuppressMessage("Reliability", "CA2015", Justification = "The caller must hold the reference to the memory object.")]
        ~AnonymousPage() => Dispose(disposing: false);
    }
}