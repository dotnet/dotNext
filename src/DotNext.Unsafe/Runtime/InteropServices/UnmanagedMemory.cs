using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices;

/// <summary>
/// Represents a handle to the value of type <typeparamref name="T"/> allocated in the unmanaged memory.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
[StructLayout(LayoutKind.Auto)]
public struct UnmanagedMemory<T>() : IUnmanagedMemory
    where T : unmanaged
{
    /// <summary>
    /// Represents the pointer to the allocated memory.
    /// </summary>
    public readonly Pointer<T> Pointer = Pointer<T>.Allocate();

    /// <summary>
    /// Allocates a new unmanaged memory for the type <typeparamref name="T"/> and places
    /// the specified value to the allocated space.
    /// </summary>
    /// <param name="value">The value to be placed to the allocated space.</param>
    public UnmanagedMemory(T value)
        : this()
        => Pointer.Value = value;

    /// <inheritdoc/>
    Pointer<byte> IUnmanagedMemory.Pointer => Pointer.As<byte>();

    /// <summary>
    /// Releases the unmanaged memory.
    /// </summary>
    public void Dispose()
    {
        Pointer<T>.Free(Pointer);
        this = default;
    }

    /// <inheritdoc/>
    unsafe nuint IUnmanagedMemory.Size => (uint)sizeof(T);

    /// <inheritdoc/>
    readonly Span<byte> IUnmanagedMemory.Bytes => Pointer.Bytes;

    /// <inheritdoc/>
    public override string ToString() => Pointer.ToString();
}