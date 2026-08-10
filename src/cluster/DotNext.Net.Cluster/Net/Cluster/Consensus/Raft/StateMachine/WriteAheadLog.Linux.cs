using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;
using Numerics;

partial class WriteAheadLog
{
    /// <summary>
    /// Represents a page of private memory that is flushed to disk with unbuffered (direct) I/O,
    /// bypassing the OS page cache.
    /// </summary>
    [SupportedOSPlatform("linux")]
    private sealed class LinuxDirectPage : AnonymousPageBase
    {
        private readonly uint sectorSize;
        private readonly unsafe delegate*unmanaged<void*, int, int, int> openFileFunction;

        public unsafe LinuxDirectPage(int pageSize, nuint alignment, uint sectorSize, delegate*unmanaged<void*, int, int, int> openFileFunction)
            : base(pageSize, alignment)
        {
            this.sectorSize = sectorSize;
            this.openFileFunction = openFileFunction;
        }

        protected override SafeFileHandle OpenRead(string fileName)
            => OpenDirect(fileName, forWriting: false);

        protected override ValueTask FlushAsync(DirectoryInfo directory, uint pageIndex, int offset, int length, CancellationToken token)
        {
            var task = ValueTask.CompletedTask;
            var handle = OpenDirect(GetPageFileName(directory, pageIndex), forWriting: true);
            try
            {
                EnsureFileSize(handle);

                // Unbuffered I/O requires the file offset, the transfer length, and the buffer
                // address to be aligned to the filesystem block size. The buffer address is
                // already aligned (AlignedAlloc), so only offset/length need to be rounded
                // to a block boundary; the extra bytes covered by the rounding are always
                // valid, in-bounds page contents.
                var alignedOffset = ((uint)offset).RoundDown(sectorSize);
                var alignedEnd = ((uint)(offset + length)).RoundUp(sectorSize);

                var buffer = Memory[(int)alignedOffset..(int)alignedEnd];
                RandomAccess.Write(handle, buffer.Span, alignedOffset);
            }
            catch (Exception e)
            {
                task = ValueTask.FromException(e);
            }
            finally
            {
                handle.Dispose();
            }

            return task;
        }
        
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private unsafe SafeFileHandle OpenDirect(ReadOnlySpan<char> path, bool forWriting)
        {
            const int oRdonly = 0;
            const int oWronly = 1;
            const int oCreat = 0x40;
            const int oDsync = 0x1000; // as defined for x86, x86_64, arm, arm64, and riscv
            const int oDirect = 0x4000; // as defined for x86, x86_64, arm, arm64, and riscv

            // O_DSYNC matches the durability guarantee FileOptions.WriteThrough provides
            // on the buffered path (verified to map to O_SYNC on Linux); without it, O_DIRECT
            // alone only bypasses the page cache and doesn't force the write to be durable.
            var flags = oDirect | (forWriting ? oWronly | oCreat | oDsync : oRdonly);
            const int mode = 0x1B6; // 0o666, subject to umask

            var byteCount = Encoding.UTF8.GetByteCount(path) + 1;
            using var pathBuffer = (uint)byteCount <= (uint)SpanOwner<byte>.StackallocThreshold
                ? stackalloc byte[byteCount]
                : new SpanOwner<byte>(byteCount);
            Encoding.UTF8.GetBytes(path, pathBuffer.Span);
            pathBuffer[^1] = 0;

            int fd;
            fixed (byte* pathPtr = pathBuffer)
            {
                fd = openFileFunction(pathPtr, flags, mode);
            }

            return fd >= 0
                ? new SafeFileHandle(fd, ownsHandle: true)
                : throw new ExternalException($"Unable to open '{path}' with O_DIRECT.", fd);
        }
    }

    [SupportedOSPlatform("linux")]
    private sealed class LinuxDirectPageManager : AnonymousPageManager<LinuxDirectPage>
    {
        private readonly uint sectorSize;
        private readonly nuint alignment;
        private readonly unsafe delegate*unmanaged<nint, nint, int, int> madvise;
        private readonly unsafe delegate*unmanaged<void*, int, int, int> openFileFunction;

        public unsafe LinuxDirectPageManager(DirectoryInfo location, int pageSize)
            : base(location, pageSize, out var pages)
        {
            alignment = AnonymousPageManager.GetPageAlignment(pageSize, out madvise);

            var programHandle = NativeLibrary.GetMainProgramHandle();

            // detect sector size
            sectorSize = NativeLibrary.TryGetExport(programHandle, "statvfs", out var statvfs)
                ? (uint)GetSectorSize(location, (delegate*unmanaged<byte*, void*, int>)statvfs)
                : (uint)Environment.SystemPageSize;

            if (NativeLibrary.TryGetExport(programHandle, "open", out var openFn))
            {
                openFileFunction = (delegate*unmanaged<void*, int, int, int>)openFn;
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            if ((uint)pageSize % sectorSize is not 0)
                throw new ArgumentOutOfRangeException(nameof(pageSize));

            Initialize(location, pages);

            [SkipLocalsInit]
            [MethodImpl(MethodImplOptions.NoInlining)]
            static nuint GetSectorSize(DirectoryInfo location, delegate*unmanaged<byte*, void*, int> statvfs)
            {
                var path = location.FullName;
                var byteCount = Encoding.UTF8.GetByteCount(path) + 1;
                using var pathBuffer = (uint)byteCount <= (uint)SpanOwner<byte>.StackallocThreshold
                    ? stackalloc byte[byteCount]
                    : new SpanOwner<byte>(byteCount);
                Encoding.UTF8.GetBytes(path, pathBuffer.Span);
                pathBuffer[^1] = 0;

                // f_bsize is the first member of struct statvfs on both glibc and musl, represented
                // as a native "unsigned long" in both. The rest of the layout differs between the
                // two, so the buffer only needs to be large enough for the real struct to be written
                // into without corrupting the stack; nothing past the first field is inspected.
                var resultPtr = stackalloc byte[256];

                int errorCode;
                fixed (byte* pathPtr = pathBuffer)
                {
                    errorCode = statvfs(pathPtr, resultPtr);
                }

                return errorCode is 0 ? *(nuint*)resultPtr : (uint)Environment.SystemPageSize;
            }
        }

        protected override unsafe LinuxDirectPage CreatePage(bool reusable)
        {
            var page = new LinuxDirectPage(PageSize, alignment, sectorSize, openFileFunction);
            if (reusable && madvise is not null)
            {
                page.ConvertToHugePage(madvise);
            }

            return page;
        }

        protected override unsafe void ReleasePage(LinuxDirectPage page)
            => ReleasePage(page, madvise is null); // THP splits the page on discard, skip this behavior for HugePages
    }
}
