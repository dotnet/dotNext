using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

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

        public override void Populate(DirectoryInfo location, uint pageIndex)
        {
            using var handle = File.OpenHandle(GetPageFileName(location, pageIndex), options: NoBuffering);
            RandomAccess.Read(handle, GetSpan(), fileOffset: 0L);
        }

        protected override async ValueTask FlushAsync(DirectoryInfo directory, uint pageIndex, int offset, int length, CancellationToken token)
        {
            using var handle = File.OpenHandle(GetPageFileName(directory, pageIndex),
                FileMode.OpenOrCreate,
                FileAccess.Write,
                options: FileOptions.Asynchronous | FileOptions.WriteThrough | NoBuffering);

            if (RandomAccess.GetLength(handle) is 0U)
                RandomAccess.SetLength(handle, Size);

            // Unbuffered I/O requires the file offset, the transfer length, and the buffer
            // address to be aligned to the volume's sector size. The buffer address is
            // already aligned (AlignedAlloc), so only offset/length need to be rounded
            // to a sector boundary; the extra bytes covered by the rounding are always
            // valid, in-bounds page contents.
            var alignedOffset = ((uint)offset).RoundDown(sectorSize);
            var alignedEnd = ((uint)(offset + length)).RoundUp(sectorSize);

            var buffer = Memory[(int)alignedOffset..(int)alignedEnd];
            await RandomAccess.WriteAsync(handle, buffer, alignedOffset, token).ConfigureAwait(false);
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