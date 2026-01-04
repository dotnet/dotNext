using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace DotNext.Runtime.InteropServices;

using CompilerServices;

/// <summary>
/// Represents an opaque value that can be passed
/// to unmanaged code as <see cref="nint"/>.
/// </summary>
/// <remarks>
/// The opaque value can be used to pass the value from the managed code to the unmanaged code
/// that keeps the callback to the managed code through the shim method marked as <see cref="UnmanagedCallersOnlyAttribute"/>.
/// </remarks>
/// <typeparam name="T">The underlying type.</typeparam>
/// <seealso cref="UnmanagedCallersOnlyAttribute"/>
[StructLayout(LayoutKind.Auto)]
[NativeMarshalling(typeof(OpaqueValueMarshaller<>))]
public readonly record struct OpaqueValue<T> : IDisposable
    where T : notnull
{
    // For reference types and structs with reference fields, it's a GC handle
    // For blittable structs, it's a pointer to the aligned memory containing the value
    // For byte, sbyte, int, uint, it's the value itself
    // For long, ulong on 64-bit system, it's the value itself
    internal readonly nint valueOrHandle;

    /// <summary>
    /// Creates a copy of the value or reference and makes it available from within the unmanaged code.
    /// </summary>
    /// <param name="value">The value that can be accessed from the unmanaged code.</param>
    public OpaqueValue(T? value)
    {
        if (value is null)
        {
            valueOrHandle = 0;
        }
        else if (ContainsReferences)
        {
            valueOrHandle = GCHandle<object>.ToIntPtr(new(value));
        }
        else if (CanBeInlined)
        {
            Unsafe.As<nint, T>(ref valueOrHandle) = value;
        }
        else
        {
            unsafe
            {
                var pointer = NativeMemory.AlignedAlloc((uint)Unsafe.SizeOf<T>(), (uint)Unsafe.AlignOf<T>());
                valueOrHandle = (nint)pointer;
                Unsafe.AsRef<T>(pointer) = value;
            }
        }
    }

    internal OpaqueValue(nint handle) => valueOrHandle = handle;
    
    internal static bool ContainsReferences => RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    internal static bool CanBeInlined => Unsafe.SizeOf<T>() <= nint.Size;

    /// <summary>
    /// Releases the underlying
    /// </summary>
    public void Dispose()
    {
        if (ContainsReferences)
        {
            if (GCHandle<object>.FromIntPtr(valueOrHandle) is { IsAllocated: true } gch)
                gch.Dispose();
        }
        else if (CanBeInlined)
        {
            // nothing to dispose
        }
        else
        {
            unsafe
            {
                var pointer = valueOrHandle.ToPointer();
                if (pointer is not null)
                    NativeMemory.AlignedFree(pointer);
            }
        }
    }

    /// <inheritdoc/>
    public override string ToString() => valueOrHandle.ToString();
}

/// <summary>
/// Provides access to the opaque value from the unmanaged code.
/// </summary>
public static class OpaqueValue
{
    /// <summary>
    /// Unboxes the value.
    /// </summary>
    /// <param name="value">The opaque value.</param>
    /// <typeparam name="T">The value type.</typeparam>
    /// <returns>A reference to the boxed value; or <see cref="Unsafe.NullRef{T}"/> if <paramref name="value"/> is not allocated.</returns>
    public static ref readonly T Unbox<T>(this in OpaqueValue<T> value)
        where T : struct
    {
        if (OpaqueValue<T>.ContainsReferences)
        {
            return ref GCHandle<object>.FromIntPtr(value.valueOrHandle) is { IsAllocated: true, Target: { } target and T }
                ? ref Unsafe.Unbox<T>(target)
                : ref Unsafe.NullRef<T>();
        }

        if (OpaqueValue<T>.CanBeInlined)
        {
            return ref Unsafe.As<nint, T>(ref Unsafe.AsRef(in value.valueOrHandle));
        }

        unsafe
        {
            return ref Unsafe.AsRef<T>(value.valueOrHandle.ToPointer());
        }
    }
    
    /// <summary>
    /// Extends <see cref="OpaqueValue{T}"/> type.
    /// </summary>
    /// <param name="value">The opaque reference.</param>
    /// <typeparam name="T">The reference type.</typeparam>
    extension<T>(OpaqueValue<T> value) where T : class
    {
        /// <summary>
        /// Gets the value of the reference type.
        /// </summary>
        /// <value>The extracted value; or <see langword="null"/> if <paramref name="value"/> is not allocated.</value>
        public T? Value => GCHandle<object>.FromIntPtr(value.valueOrHandle) is { IsAllocated: true, Target: T target }
            ? target
            : null;
    }
}