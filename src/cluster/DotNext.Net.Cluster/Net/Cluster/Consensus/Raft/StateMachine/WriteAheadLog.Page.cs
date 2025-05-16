using System.Buffers;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using Microsoft.Win32.SafeHandles;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using IO;

partial class WriteAheadLog
{
    /// <summary>
    /// Represents memory-mapped page of memory.
    /// </summary>
    private sealed class Page : MemoryManager<byte>, IFlushable
    {
        private readonly string fileName;
        private readonly SafeFileHandle fileHandle;
        private readonly IDisposable viewHandle;
        private readonly MemoryMappedViewAccessor accessor;

        public Page(DirectoryInfo directory, uint pageIndex, int pageSize)
        {
            Debug.Assert(pageSize % Environment.SystemPageSize is 0);

            fileName = GetPageFileName(directory, pageIndex);
            long preallocationSize;
            FileMode mode;
            if (File.Exists(fileName))
            {
                preallocationSize = 0L;
                mode = FileMode.Open;
            }
            else
            {
                preallocationSize = pageSize;
                mode = FileMode.CreateNew;
            }

            fileHandle = File.OpenHandle(fileName, mode, FileAccess.ReadWrite, preallocationSize: preallocationSize);
            File.SetAttributes(fileHandle, FileAttributes.NotContentIndexed);
            
            var mappedHandle = MemoryMappedFile.CreateFromFile(fileHandle, mapName: null, pageSize, MemoryMappedFileAccess.ReadWrite,
                HandleInheritability.None, leaveOpen: true);
            accessor = mappedHandle.CreateViewAccessor(0L, pageSize, MemoryMappedFileAccess.ReadWrite);
            viewHandle = mappedHandle;
            
            static string GetPageFileName(DirectoryInfo directory, uint pageIndex)
                => Path.Combine(directory.FullName, pageIndex.ToString(InvariantCulture));
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
            File.SetLastWriteTime(fileHandle, DateTime.Now);
            RandomAccess.FlushToDisk(fileHandle); // update file metadata and size
        }

        public override unsafe Span<byte> GetSpan() =>
            new(Pointer.ToPointer(), (int)accessor.Capacity);

        public override unsafe MemoryHandle Pin(int elementIndex = 0)
            => new((Pointer + elementIndex).ToPointer());

        public override void Unpin()
        {
            // nothing to do
        }

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
}