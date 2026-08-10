using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Numerics;

partial class WriteAheadLog
{
    /// <summary>
    /// Represents a page of private memory that is flushed to disk with unbuffered (direct) I/O,
    /// bypassing the OS page cache.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private sealed class WindowsDirectPage(int pageSize, uint sectorSize) : AnonymousPageBase(pageSize, (uint)Environment.SystemPageSize)
    {
        // FILE_FLAG_NO_BUFFERING, not exposed by System.IO.FileOptions but recognized by
        // CreateFileW on Windows because FileOptions bits are passed through verbatim.
        private const FileOptions NoBuffering = (FileOptions)0x2000_0000;

        protected override SafeFileHandle OpenRead(string fileName)
            => File.OpenHandle(fileName, options: NoBuffering);

        protected override async ValueTask FlushAsync(string fileName, int offset, int length, CancellationToken token)
        {
            using var handle = File.OpenHandle(fileName,
                FileMode.OpenOrCreate,
                FileAccess.Write,
                options: FileOptions.Asynchronous | FileOptions.WriteThrough | NoBuffering);

            EnsureFileSize(handle);
            await FlushAsync(handle, offset, length, token).ConfigureAwait(false);
        }

        private ValueTask FlushAsync(SafeFileHandle handle, int offset, int length, CancellationToken token)
        {
            // Unbuffered I/O requires the file offset, the transfer length, and the buffer
            // address to be aligned to the volume's sector size. The buffer address is
            // already aligned (AlignedAlloc), so only offset/length need to be rounded
            // to a sector boundary; the extra bytes covered by the rounding are always
            // valid, in-bounds page contents.
            var (alignedOffset, alignedEnd) = Align(offset, length, sectorSize);

            return RandomAccess.WriteAsync(handle, Memory[alignedOffset..alignedEnd], alignedOffset, token);
        }
    }
    
    [SupportedOSPlatform("windows")]
    private sealed partial class WindowsDirectPageManager : AnonymousPageManager<WindowsDirectPage>
    {
        private readonly uint sectorSize;

        public WindowsDirectPageManager(DirectoryInfo location, int pageSize)
            : base(location, pageSize, out var pages)
        {
            sectorSize = GetSectorSize(location);
            if ((uint)pageSize % sectorSize is not 0)
                throw new ArgumentOutOfRangeException(nameof(pageSize));

            Initialize(location, pages);

            static uint GetSectorSize(DirectoryInfo location)
                => GetDiskFreeSpace(location.Root.FullName, out _, out var bytesPerSector, out _, out _)
                    ? bytesPerSector
                    : throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        [LibraryImport("kernel32.dll", EntryPoint = "GetDiskFreeSpaceW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetDiskFreeSpace(ReadOnlySpan<char> lpRootPathName, out uint lpSectorsPerCluster, out uint lpBytesPerSector, out uint lpNumberOfFreeClusters, out uint lpTotalNumberOfClusters);

        protected override WindowsDirectPage CreatePage(bool reusable) => new(PageSize, sectorSize);
    }
}