using System.Diagnostics.CodeAnalysis;
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
    internal readonly nint handle;

    /// <summary>
    /// Creates a copy of the value or reference and makes it available from within the unmanaged code.
    /// </summary>
    /// <param name="value">The value that can be accessed from the unmanaged code.</param>
    public OpaqueValue(T? value)
    {
        if (value is null)
        {
            handle = 0;
        }
        else if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            handle = GCHandle<object>.ToIntPtr(new(value));
        }
        else
        {
            unsafe
            {
                var pointer = Unsafe.IsNaturallyAligned<T>()
                    ? NativeMemory.Alloc((uint)Unsafe.SizeOf<T>())
                    : NativeMemory.AlignedAlloc((uint)Unsafe.SizeOf<T>(), (uint)Unsafe.AlignOf<T>());
                
                handle = (nint)pointer;
                Unsafe.AsRef<T>(pointer) = value;
            }
        }
    }

    internal OpaqueValue(nint handle) => this.handle = handle;

    /// <summary>
    /// Releases the underlying storage for the value.
    /// </summary>
    /// <remarks>
    /// This method is not idempotent and should not be called twice.
    /// </remarks>
    public unsafe void Dispose()
    {
        if (handle is 0)
        {
            // nothing to do
        }
        else if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            GCHandle<object>.FromIntPtr(handle).Dispose();
        }
        else if (Unsafe.IsNaturallyAligned<T>())
        {
            NativeMemory.Free(handle.ToPointer());
        }
        else
        {
            NativeMemory.AlignedFree(handle.ToPointer());
        }
    }

    /// <inheritdoc/>
    public override string ToString() => handle.ToString();
}

/// <summary>
/// Provides extensions for <see cref="OpaqueValue{T}"/> when its underlying type is of value type.
/// </summary>
public static class OpaqueValueType
{
    /// <summary>
    /// Extends <see cref="OpaqueValue{T}"/> type.
    /// </summary>
    /// <param name="value">The opaque reference.</param>
    /// <typeparam name="T">The value type.</typeparam>
    extension<T>(OpaqueValue<T> value) where T : struct
    {
        /// <summary>
        /// Gets mutable reference to the value.
        /// </summary>
        public unsafe ref T Value
        {
            get
            {
                if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                    return ref Unsafe.AsRef<T>(value.handle.ToPointer());

                return ref GCHandle<object>.FromIntPtr(value.handle) is { IsAllocated: true, Target: { } target and T }
                    ? ref Unsafe.Unbox<T>(target)
                    : ref Unsafe.NullRef<T>();
            }
        }
    }
}

/// <summary>
/// Provides extensions for <see cref="OpaqueValue{T}"/> when its underlying type is of reference type.
/// </summary>
public static class OpaqueReferenceType
{
    /// <summary>
    /// Extends <see cref="OpaqueValue{T}"/> type.
    /// </summary>
    /// <param name="opaque">The opaque reference.</param>
    /// <typeparam name="T">The reference type.</typeparam>
    extension<T>(OpaqueValue<T> opaque) where T : class
    {
        /// <summary>
        /// Gets or sets the value of the reference type.
        /// </summary>
        /// <value>The extracted value; or <see langword="null"/> if <paramref name="opaque"/> is not allocated.</value>
        [DisallowNull]
        public T? Value
        {
            get => GCHandle<object>.FromIntPtr(opaque.handle) is { IsAllocated: true, Target: T target }
                ? target
                : null;
            set => GCHandle<object>.FromIntPtr(opaque.handle).Target = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
}