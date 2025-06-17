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
        public const int MinPageSize = 4096;
        private readonly string fileName;
        private readonly SafeFileHandle fileHandle;
        private readonly IDisposable viewHandle;
        private readonly MemoryMappedViewAccessor accessor;

        public Page(DirectoryInfo directory, uint pageIndex, int pageSize)
        {
            Debug.Assert(pageSize % MinPageSize is 0);

            fileName = GetPageFileName(directory, pageIndex);

            const FileAccess fileAccess = FileAccess.ReadWrite;
            fileHandle = File.OpenHandle(fileName, FileMode.OpenOrCreate, fileAccess);

            var mappedHandle = MemoryMappedFile.CreateFromFile(fileHandle, mapName: null, pageSize, MemoryMappedFileAccess.ReadWrite,
                HandleInheritability.None, leaveOpen: true);
            accessor = mappedHandle.CreateViewAccessor(0L, pageSize, MemoryMappedFileAccess.ReadWrite);
            viewHandle = mappedHandle;
            
            static string GetPageFileName(DirectoryInfo directory, uint pageIndex)
                => Path.Combine(directory.FullName, pageIndex.ToString(InvariantCulture));

            static bool AlreadyExists(int hresult, string fileName)
            {
                // EEXIST errno-base.h
                const int EEXIST = 17;
                if (OperatingSystem.IsLinux())
                    return hresult is EEXIST;

                // https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-
                const int ERROR_ALREADY_EXISTS = unchecked((int)0x80070000 | 0xB7);
                if (OperatingSystem.IsWindows())
                    return hresult is ERROR_ALREADY_EXISTS;

                return File.Exists(fileName);
            }
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