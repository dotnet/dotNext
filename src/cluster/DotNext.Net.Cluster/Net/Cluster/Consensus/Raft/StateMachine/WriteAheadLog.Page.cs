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
using Numerics;

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
            File.SetAttributes(fileHandle, FileAttributes.NotContentIndexed);

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
    
    private abstract class AnonymousPageBase : Page
    {
        private readonly int pageSize;
        private unsafe void* address;
        
        protected unsafe AnonymousPageBase(int pageSize, nuint alignment)
        {
            Debug.Assert(pageSize % MinSize is 0);
            Debug.Assert((uint)pageSize % alignment is 0);

            address = NativeMemory.AlignedAlloc((uint)pageSize, alignment);
            
            this.pageSize = pageSize;
            PoolIndex = -1;
        }

        protected void EnsureFileSize(SafeFileHandle handle)
        {
            if (RandomAccess.GetLength(handle) is 0U)
                RandomAccess.SetLength(handle, pageSize);
        }

        protected static (int Start, int End) Align(int offset, int length, uint sectorSize) => new()
        {
            Start = (int)((uint)offset).RoundDown(sectorSize),
            End = (int)((uint)offset + (uint)length).RoundUp(sectorSize),
        };

        public unsafe void Clear() => NativeMemory.Clear(address, (uint)pageSize);

        public void Discard()
        {
            if ((pageSize & (Environment.SystemPageSize - 1)) is 0)
            {
                NativeMemory.Discard(GetSpan());
            }
        }

        public int PoolIndex { get; set; }

        protected virtual SafeFileHandle OpenRead(string fileName)
            => File.OpenHandle(fileName, options: FileOptions.SequentialScan);

        public void Populate(DirectoryInfo location, uint pageIndex)
        {
            using var handle = OpenRead(GetPageFileName(location, pageIndex));
            RandomAccess.Read(handle, GetSpan(), fileOffset: 0L);
        }
        
        public static void Delete(DirectoryInfo directory, uint pageIndex)
            => File.Delete(GetPageFileName(directory, pageIndex));

        protected abstract ValueTask FlushAsync(string fileName, int offset, int length, CancellationToken token);

        public ValueTask FlushAsync(DirectoryInfo directory, uint pageIndex, Range range, CancellationToken token)
        {
            var (offset, length) = range.GetOffsetAndLength(pageSize);
            return FlushAsync(GetPageFileName(directory, pageIndex), offset, length, token);
        }

        public sealed override unsafe Span<byte> GetSpan() => new(address, pageSize);

        public sealed override Memory<byte> Memory => CreateMemory(pageSize);
        
        public unsafe void ConvertToHugePage(delegate*unmanaged<nint, nint, int, int> madvise)
        {
            Debug.Assert(madvise is not null);
            
            const int MADV_HUGEPAGE = 14;
            var errorCode = madvise((nint)address, pageSize, MADV_HUGEPAGE);
            Debug.Assert(errorCode is 0);
        }

        protected override unsafe void Dispose(bool disposing)
        {
            if (address is not null)
            {
                NativeMemory.AlignedFree(address);
                address = null;
            }
        }

        [SuppressMessage("Reliability", "CA2015", Justification = "The caller must hold the reference to the memory object.")]
        ~AnonymousPageBase() => Dispose(disposing: false);
    }
    
    /// <summary>
    /// Represents memory-mapped page of memory.
    /// </summary>
    private sealed class AnonymousPage(int pageSize, nuint alignment) : AnonymousPageBase(pageSize, alignment)
    {
        protected override async ValueTask FlushAsync(string fileName, int offset, int length, CancellationToken token)
        {
            using var handle = File.OpenHandle(fileName,
                FileMode.OpenOrCreate,
                FileAccess.Write,
                options: FileOptions.WriteThrough | FileOptions.Asynchronous);

            EnsureFileSize(handle);

            await RandomAccess.WriteAsync(
                    handle,
                    Memory.Slice(offset, length),
                    offset,
                    token)
                .ConfigureAwait(false);
        }
    }
}