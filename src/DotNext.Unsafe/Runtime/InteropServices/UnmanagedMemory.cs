using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices;

using Runtime;

/// <summary>
/// Represents a handle to the value of type <typeparamref name="T"/> allocated in the unmanaged memory.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
[StructLayout(LayoutKind.Auto)]
public unsafe struct UnmanagedMemory<T> : IUnmanagedMemory
    where T : unmanaged
{
    private T* address;

    /// <summary>
    /// Initializes a new handle.
    /// </summary>
    public UnmanagedMemory()
    {
        address = (T*)NativeMemory.AlignedAlloc((uint)sizeof(T), (uint)Intrinsics.AlignOf<T>());
    }

    /// <summary>
    /// Gets a value indicating whether this handle is allocated.
    /// </summary>
    public readonly bool IsAllocated => address is not null;

    /// <summary>
    /// Gets a reference to the value.
    /// </summary>
    public readonly ref T Value => ref *address;

    /// <summary>
    /// Releases the unmanaged memory.
    /// </summary>
    public void Dispose()
    {
        if (address is not null)
        {
            NativeMemory.AlignedFree(address);
            address = null;
        }
    }

    /// <inheritdoc/>
    readonly nuint IUnmanagedMemory.Size => (uint)sizeof(T);

    /// <inheritdoc/>
    public readonly Pointer<byte> Pointer => new((nuint)address);

    /// <inheritdoc/>
    public readonly Span<byte> Bytes => address is not null ? new(address, sizeof(T)) : Span<byte>.Empty;

    /// <inheritdoc/>
    public override string? ToString() => IsAllocated ? Value.ToString() : null;

    /// <summary>
    /// Gets the span over the unmanaged memory block of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="memory">The memory block.</param>
    /// <returns>The span over the unmanaged memory block.</returns>
    public static implicit operator Span<byte>(UnmanagedMemory<T> memory) => memory.Bytes;

    /// <summary>
    /// Gets the reference to the unmanaged memory.
    /// </summary>
    /// <param name="memory">The memory block.</param>
    /// <returns>The reference to the unmanaged memory.</returns>
    public static implicit operator ValueReference<T>(UnmanagedMemory<T> memory)
        => memory.IsAllocated ? new(ref memory.Value) : default;
}