using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

using Numerics;
using Runtime.CompilerServices;

partial class UnmanagedMemory
{
    /// <summary>
    /// Extends <see cref="NativeMemory"/> type.
    /// </summary>
    extension(NativeMemory)
    {
        /// <summary>
        /// Allocates a specified number of system pages.
        /// </summary>
        /// <param name="pageCount">The number of system pages to be allocated.</param>
        /// <returns>A memory owner that represents allocated system pages.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageCount"/> is negative or zero.</exception>
        public static IMemoryOwner<byte> AllocateSystemPages(int pageCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(pageCount);

            return pageCount is 0 ? UnmanagedMemory<byte>.Empty() : new SystemPageManager(pageCount);
        }

        /// <summary>
        /// Allocates page-aligned memory.
        /// </summary>
        /// <param name="size">The number of bytes to be allocated.</param>
        /// <param name="roundUpSize"><see langword="true"/> to round up the <paramref name="size"/> to the page size; otherwise, <see langword="false"/>.</param>
        /// <returns>A memory owner that represents page-aligned memory block.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative or zero.</exception>
        [CLSCompliant(false)]
        public static IMemoryOwner<byte> AllocatePageAlignedMemory(int size, bool roundUpSize = false)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(size);

            return size is 0 ? UnmanagedMemory<byte>.Empty() : new SystemPageManager(size, roundUpSize);
        }

        /// <summary>
        /// Calculates the page aligned offset for the specified memory block.
        /// </summary>
        /// <param name="memory">The memory block.</param>
        /// <param name="offset">The offset to the page-aligned element.</param>
        /// <returns><see langword="true"/> if the <paramref name="offset"/> within the memory range; otherwise, <see langword="false"/>.</returns>
        public static unsafe bool GetPageAlignedOffset(ReadOnlySpan<byte> memory, out int offset)
        {
            nuint pageSize = (uint)Environment.SystemPageSize;
            fixed (void* ptr = memory)
            {
                var address = (nuint)ptr;
                var alignedAddress = address - (address & (pageSize - 1));
                alignedAddress = address == alignedAddress ? alignedAddress : alignedAddress + pageSize;

                offset = (int)(alignedAddress - address);
            }

            return offset < memory.Length;
        }

        /// <summary>
        /// Returns the backing RAM back to the OS.
        /// </summary>
        /// <param name="memoryBlock">A region of the virtual memory.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The length or the offset of <paramref name="memoryBlock"/> is not page-aligned.
        /// </exception>
        /// <exception cref="ArgumentException"><paramref name="memoryBlock"/> represents stack-allocated memory.</exception>
        /// <seealso cref="AllocateSystemPages"/>
        public static unsafe void Discard<T>(Span<T> memoryBlock)
            where T : unmanaged
        {
            if (RuntimeHelpers.IsStackDetectionSupported && memoryBlock.IsStackAllocated)
                throw new ArgumentException(ExceptionMessages.UnableToDiscardMemory, nameof(memoryBlock));
            
            fixed (void* ptr = memoryBlock)
            {
                SystemPageManager.Discard((nint)ptr, (nint)sizeof(T) * memoryBlock.Length);
            }
        }
    }
}

file sealed unsafe class SystemPageManager : UnmanagedMemory<byte>
{
    private static readonly nint VirtualMemoryManagementFunc;

    static SystemPageManager()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // Since glibc 2.6, POSIX_MADV_DONTNEED is treated as a no-op due to destructive semantics of the flag.
            // Therefore, madvise is used instead.
            NativeLibrary.TryGetExport(NativeLibrary.GetMainProgramHandle(), "madvise", out VirtualMemoryManagementFunc);
        }
        else if (OperatingSystem.IsWindows())
        {
            if (NativeLibrary.TryLoad("kernel32.dll", out var libraryHandle))
                NativeLibrary.TryGetExport(libraryHandle, "DiscardVirtualMemory", out VirtualMemoryManagementFunc);
        }
        else
        {
            VirtualMemoryManagementFunc = 0;
        }
    }
    
    internal SystemPageManager(int pageCount)
        : base(Allocate(pageCount, out var length), length)
    {
    }

    internal SystemPageManager(int sizeInBytes, bool roundUp)
        : base(Allocate(ref sizeInBytes, roundUp), sizeInBytes)
    {
    }
    
    private static bool IsPageAligned(nint value)
        => (value & (Environment.SystemPageSize - 1)) is 0;

    internal static void Discard(nint address, nint length)
    {
        if (!IsPageAligned(address))
            throw new ArgumentOutOfRangeException(nameof(address));

        if (!IsPageAligned(length))
            throw new ArgumentOutOfRangeException(nameof(length));

        if (VirtualMemoryManagementFunc is 0)
            return;

        int errorCode;
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
        {
            const int MADV_DONTNEED = 4;

            var madvise = (delegate*unmanaged<nint, nint, int, int>)VirtualMemoryManagementFunc;
            errorCode = madvise(address, length, MADV_DONTNEED);
        }
        else if (OperatingSystem.IsWindows())
        {
            var discardVirtualMemory = (delegate*unmanaged<nint, nint, int>)VirtualMemoryManagementFunc;
            errorCode = discardVirtualMemory(address, length);
        }
        else
        {
            errorCode = 0;
        }

        if (errorCode is not 0)
            throw new ExternalException(ExceptionMessages.UnableToDiscardMemory, errorCode);
    }

    protected override void Dispose(bool disposing)
    {
        if (address is not null)
        {
            NativeMemory.AlignedFree(address);
        }

        base.Dispose(disposing);
    }

    private static byte* Allocate(int pageCount, out int length)
    {
        Debug.Assert(pageCount > 0);

        var pageSize = Environment.SystemPageSize;
        length = checked(pageSize * pageCount);
        return (byte*)NativeMemory.AlignedAlloc((uint)length, (uint)pageSize);
    }

    private static byte* Allocate(ref int sizeInBytes, bool roundUp)
    {
        var size = (uint)sizeInBytes;
        var pageSize = (uint)Environment.SystemPageSize;
        if (roundUp)
        {
            size = size.RoundUp(pageSize);
            sizeInBytes = checked((int)size);
        }

        return (byte*)NativeMemory.AlignedAlloc(size, pageSize);
    }

    [SuppressMessage("Reliability", "CA2015", Justification = "The caller must hold the reference to the memory object.")]
    ~SystemPageManager() => Dispose(disposing: false);
}