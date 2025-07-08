using System.ComponentModel;
using System.Runtime.InteropServices.Marshalling;

namespace DotNext.Runtime.InteropServices;

/// <summary>
/// Represents marshaller for <see cref="Pointer{T}"/> data type.
/// </summary>
/// <typeparam name="T">The pointer type.</typeparam>
[CustomMarshaller(typeof(Pointer<>), MarshalMode.ManagedToUnmanagedIn, typeof(PointerMarshaller<>))]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class PointerMarshaller<T>
    where T : unmanaged
{
    /// <summary>
    /// Converts a pointer to the address.
    /// </summary>
    /// <param name="ptr">The pointer value.</param>
    /// <returns>The address of the pointer.</returns>
    public static nuint ConvertToUnmanaged(Pointer<T> ptr) => ptr;
}