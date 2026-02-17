using System.ComponentModel;
using System.Runtime.InteropServices.Marshalling;

namespace DotNext.Runtime.InteropServices;

/// <summary>
/// Represents marshaller for <see cref="Pointer{T}"/> data type.
/// </summary>
/// <typeparam name="T">The pointer type.</typeparam>
[CustomMarshaller(typeof(OpaqueValue<>), MarshalMode.ManagedToUnmanagedIn, typeof(OpaqueValueMarshaller<>))]
[CustomMarshaller(typeof(OpaqueValue<>), MarshalMode.ManagedToUnmanagedOut, typeof(OpaqueValueMarshaller<>))]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class OpaqueValueMarshaller<T>
    where T : notnull
{
    /// <summary>
    /// Converts a pointer to the address.
    /// </summary>
    /// <param name="value">The pointer value.</param>
    /// <returns>The address of the pointer.</returns>
    public static nint ConvertToUnmanaged(OpaqueValue<T> value) => value.handle;

    /// <summary>
    /// Converts an address to the pointer.
    /// </summary>
    /// <param name="handle">The address of the pointer.</param>
    /// <returns>The typed pointer.</returns>
    public static OpaqueValue<T> ConvertToManaged(nint handle) => new(handle);
}