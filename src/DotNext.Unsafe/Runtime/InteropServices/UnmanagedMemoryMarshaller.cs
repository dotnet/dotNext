using System.ComponentModel;
using System.Runtime.InteropServices.Marshalling;

namespace DotNext.Runtime.InteropServices;

/// <summary>
/// Represents a marshaller for <see cref="IUnmanagedMemory"/> interface.
/// </summary>
[CustomMarshaller(typeof(IUnmanagedMemory), MarshalMode.ManagedToUnmanagedIn, typeof(UnmanagedMemoryMarshaller))]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class UnmanagedMemoryMarshaller
{
    /// <summary>
    /// Converts unmanaged memory reference to the address.
    /// </summary>
    /// <param name="memory">The pointer value.</param>
    /// <returns>The address of the pointer.</returns>
    [CLSCompliant(false)]
    public static nint ConvertToUnmanaged(IUnmanagedMemory memory) => memory.Pointer;
}

/// <summary>
/// Represents a marshaller for <see cref="IUnmanagedMemory{T}"/> interface.
/// </summary>
[CustomMarshaller(typeof(IUnmanagedMemory<>), MarshalMode.ManagedToUnmanagedIn, typeof(UnmanagedMemoryMarshaller<>))]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class UnmanagedMemoryMarshaller<T>
    where T : unmanaged
{
    /// <summary>
    /// Converts unmanaged memory reference to the address.
    /// </summary>
    /// <param name="memory">The pointer value.</param>
    /// <returns>The address of the pointer.</returns>
    [CLSCompliant(false)]
    public static nint ConvertToUnmanaged(IUnmanagedMemory<T> memory) => memory.Pointer;
}