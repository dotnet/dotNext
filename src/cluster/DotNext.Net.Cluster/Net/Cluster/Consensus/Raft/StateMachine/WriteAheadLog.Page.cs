using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;

partial class WriteAheadLog
{
    private abstract class Page : MemoryManager<byte>
    {
        public const int MinSize = 4096;
        
        protected static string GetPageFileName(DirectoryInfo directory, uint pageIndex)
            => Path.Combine(directory.FullName, pageIndex.ToString(InvariantCulture));
        
        public sealed override void Unpin()
        {
            // nothing to do
        }

        public sealed override unsafe MemoryHandle Pin(int elementIndex = 0)
            => new(Unsafe.AsPointer(ref GetSpan()[elementIndex]));
    }
    
    private sealed class MemoryMappedPage : Page
    {
        private readonly string fileName;
        private readonly SafeFileHandle fileHandle;
        private readonly IDisposable viewHandle;
        private readonly MemoryMappedViewAccessor accessor;

        public MemoryMappedPage(DirectoryInfo directory, uint pageIndex, int pageSize)
        {
            Debug.Assert(pageSize % MinSize is 0);

            fileName = GetPageFileName(directory, pageIndex);

            const FileAccess fileAccess = FileAccess.ReadWrite;
            fileHandle = File.OpenHandle(fileName, FileMode.OpenOrCreate, fileAccess);

            var mappedHandle = MemoryMappedFile.CreateFromFile(fileHandle, mapName: null, pageSize, MemoryMappedFileAccess.ReadWrite,
                HandleInheritability.None, leaveOpen: true);
            accessor = mappedHandle.CreateViewAccessor(0L, pageSize, MemoryMappedFileAccess.ReadWrite);
            viewHandle = mappedHandle;
        }

        private nint Pointer
            => accessor.SafeMemoryMappedViewHandle.DangerousGetHandle() + (nint)accessor.PointerOffset;

        public void DisposeAndDelete()
        {
            Dispose(disposing: true);
            File.Delete(fileName);
        }

        public void Flush()
        {
            accessor.Flush();

            if (OperatingSystem.IsWindows())
            {
                File.SetLastWriteTimeUtc(fileHandle, DateTime.UtcNow);
                RandomAccess.FlushToDisk(fileHandle); // update file metadata and size
            }
        }

        public override unsafe Span<byte> GetSpan() =>
            new(Pointer.ToPointer(), (int)accessor.Capacity);

        public override Memory<byte> Memory => CreateMemory((int)accessor.Capacity);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                accessor.Dispose();
                viewHandle.Dispose();
                fileHandle.Dispose();
            }
        }
    }
    
    /// <summary>
    /// Represents memory-mapped page of memory.
    /// </summary>
    private sealed class AnonymousPage : Page
    {
        private readonly int pageSize;
        private unsafe void* address;

        public unsafe AnonymousPage(int pageSize, nuint alignment)
        {
            Debug.Assert(pageSize % MinSize is 0);
            Debug.Assert((uint)pageSize % alignment is 0);

            address = NativeMemory.AlignedAlloc((uint)pageSize, alignment);
            
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

        public ValueTask FlushAsync(DirectoryInfo directory, uint pageIndex, Range range, CancellationToken token)
        {
            var (offset, length) = range.GetOffsetAndLength(pageSize);
            return Flush(directory, pageIndex, offset, length, token);
        }

        internal unsafe void ConvertToHugePage(delegate*unmanaged<nint, nint, int, int> madise)
        {
            const int MADV_HUGEPAGE = 14;
            var errorCode = madise((nint)address, pageSize, MADV_HUGEPAGE);
            Debug.Assert(errorCode is 0);
        }

        public override unsafe Span<byte> GetSpan() => new(address, pageSize);

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