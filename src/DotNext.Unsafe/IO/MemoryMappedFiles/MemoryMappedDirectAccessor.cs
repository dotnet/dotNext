using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace DotNext.IO.MemoryMappedFiles;

using Runtime.InteropServices;

/// <summary>
/// Provides direct access to the memory-mapped file content through pointer
/// to its virtual memory.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public unsafe struct MemoryMappedDirectAccessor : IUnmanagedMemory, IFlushable
{
    private readonly MemoryMappedViewAccessor accessor;
    private readonly byte* ptr;

    internal MemoryMappedDirectAccessor(MemoryMappedViewAccessor accessor)
    {
        this.accessor = accessor;
        ptr = default;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
    }

    /// <summary>
    /// Converts the segment of the memory-mapped file.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for disposing of the returned stream.
    /// </remarks>
    /// <returns>The stream representing virtual memory of the memory-mapped file.</returns>
    public readonly Stream AsStream() => accessor is null ? Stream.Null : Pointer.AsStream(Size, accessor.GetFileAccess());

    /// <summary>
    /// Gets a value indicating that this object doesn't represent the memory-mapped file segment.
    /// </summary>
    public readonly bool IsEmpty => accessor is null;

    /// <summary>
    /// Gets the number of bytes by which the starting position of this segment is offset from the beginning of the memory-mapped file.
    /// </summary>
    public readonly long Offset => accessor?.PointerOffset ?? 0L;

    /// <summary>
    /// Gets pointer to the virtual memory of the mapped file.
    /// </summary>
    /// <value>The pointer to the memory-mapped file.</value>
    public readonly Pointer<byte> Pointer => accessor is null ? default : new Pointer<byte>(ptr + accessor.PointerOffset);

    /// <summary>
    /// Gets length of the mapped segment, in bytes.
    /// </summary>
    public readonly long Size => accessor?.Capacity ?? 0L;

    /// <summary>
    /// Represents memory-mapped file segment in the form of <see cref="Span{T}"/>.
    /// </summary>
    /// <value><see cref="Span{T}"/> representing virtual memory of the mapped file segment.</value>
    public readonly Span<byte> Bytes => Pointer.ToSpan(checked((int)accessor.Capacity));

    /// <summary>
    /// Sets all bits of allocated memory to zero.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
    public readonly void Clear()
    {
        if (ptr is null)
            throw new ObjectDisposedException(GetType().Name);

        Pointer.Clear(new IntPtr(Size));
    }

    /// <summary>
    /// Clears all buffers for this view and causes any buffered data to be written to the underlying file.
    /// </summary>
    public readonly void Flush() => accessor?.Flush();

    /// <summary>
    /// Releases virtual memory associated with the mapped file segment.
    /// </summary>
    public void Dispose()
    {
        accessor?.ReleasePointerAndDispose();
        this = default;
    }
}