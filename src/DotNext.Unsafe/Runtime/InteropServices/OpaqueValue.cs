using System.Diagnostics;
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
    // For IPointer, the handle just keeps the address without ownership
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
        else if (typeof(T) == typeof(GCHandle))
        {
            handle = GCHandle.ToIntPtr(Unsafe.BitCast<T, GCHandle>(value));
        }
        else if (default(T) is IPointer)
        {
            handle = ((IPointer)value).Address;
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
    /// Returns the cleanup action of the <see cref="CallConvCdecl"/> calling convention that releases the opaque value.
    /// </summary>
    /// <param name="opaque">The opaque value.</param>
    /// <returns>The cleanup action that can be called from the unmanaged code.</returns>
    [CLSCompliant(false)]
    public static unsafe explicit operator delegate*unmanaged[Cdecl]<nint, void>(OpaqueValue<T> opaque)
        => (delegate*unmanaged[Cdecl]<nint, void>)opaque.GetCleaner<CallConvCdecl, OpaqueValueCleaner>();

    /// <summary>
    /// Returns the cleanup action of the <see cref="CallConvStdcall"/> calling convention that releases the opaque value.
    /// </summary>
    /// <param name="opaque">The opaque value.</param>
    /// <returns>The cleanup action that can be called from the unmanaged code.</returns>
    [CLSCompliant(false)]
    public static unsafe explicit operator delegate*unmanaged[Stdcall]<nint, void>(OpaqueValue<T> opaque)
        => (delegate*unmanaged[Stdcall]<nint, void>)opaque.GetCleaner<CallConvStdcall, OpaqueValueCleaner>();

    /// <summary>
    /// Returns the cleanup action of the native calling convention for the current platform that releases the opaque value.
    /// </summary>
    /// <remarks>
    /// The returned callback has the same effect as <see cref="Dispose"/> method.
    /// </remarks>
    /// <param name="opaque">The opaque value.</param>
    /// <returns>The cleanup action that can be called from the unmanaged code.</returns>
    [CLSCompliant(false)]
    public static unsafe explicit operator delegate*unmanaged<nint, void>(OpaqueValue<T> opaque)
        => (delegate*unmanaged<nint, void>)opaque.GetCleaner<CallConvAuto, OpaqueValueCleaner>();

    private bool IsNotAllocated => default(T) is IPointer || typeof(T) == typeof(GCHandle) || handle is 0;

    private unsafe void* GetCleaner<TConvention, TCleaner>()
        where TConvention : class, new()
        where TCleaner : struct, ICleaner<TConvention>, allows ref struct
    {
        if (IsNotAllocated)
            return null;

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            return TCleaner.FreeRef;

        if (Unsafe.IsNaturallyAligned<T>())
            return TCleaner.Free;

        return TCleaner.FreeAligned;
    }

    /// <summary>
    /// Releases the underlying storage for the value.
    /// </summary>
    /// <remarks>
    /// This method is not idempotent and should not be called twice.
    /// </remarks>
    public void Dispose()
    {
        if (IsNotAllocated)
        {
            // nothing to do
        }
        else if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            ICleaner.FreeRef(handle);
        }
        else if (Unsafe.IsNaturallyAligned<T>())
        {
            ICleaner.Free(handle);
        }
        else
        {
            ICleaner.FreeAligned(handle);
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
    /// <param name="opaque">The opaque reference.</param>
    /// <typeparam name="T">The value type.</typeparam>
    extension<T>(OpaqueValue<T> opaque) where T : struct
    {
        /// <summary>
        /// Gets mutable reference to the value.
        /// </summary>
        public unsafe ref T Value
        {
            get
            {
                if (default(T) is IPointer || typeof(T) == typeof(GCHandle))
                    throw new NotSupportedException();
                
                if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                    return ref Unsafe.AsRef<T>(opaque.handle.ToPointer());

                return ref GCHandle<object>.FromIntPtr(opaque.handle) is { IsAllocated: true, Target: { } target and T }
                    ? ref Unsafe.Unbox<T>(target)
                    : ref Unsafe.NullRef<T>();
            }
        }
    }

    /// <summary>
    /// Extends <see cref="OpaqueValue{T}"/> type when it's instantiated with <see cref="Pointer{T}"/> data type.
    /// </summary>
    /// <param name="opaque">The opaque value that represents the pointer.</param>
    /// <typeparam name="T">Blittable type.</typeparam>
    extension<T>(OpaqueValue<Pointer<T>> opaque) where T : unmanaged
    {
        /// <summary>
        /// Gets a reference to the underlying value.
        /// </summary>
        public ref T Value => ref FromPointer<T, Pointer<T>>(opaque);
    }

    private static unsafe ref T FromPointer<T, TPointer>(OpaqueValue<TPointer> value)
        where T : allows ref struct
        where TPointer : struct, IPointer, ITypedReference<T>
        => ref Unsafe.AsRef<T>(value.handle.ToPointer());
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

    /// <summary>
    /// Extends <see cref="OpaqueValue{T}"/> type.
    /// </summary>
    /// <param name="opaque">The opaque value that represents a reference to the managed object represented by the handle.</param>
    extension(OpaqueValue<GCHandle> opaque)
    {
        /// <summary>
        /// Gets the referenced value.
        /// </summary>
        public object? Value
        {
            get => opaque.AsHandle().Target;
            set
            {
                var handle = opaque.AsHandle();
                handle.Target = value;
            }
        }

        private GCHandle AsHandle() => GCHandle.FromIntPtr(opaque.handle);
    }
}

internal interface ICleaner
{
    public static void FreeRef(nint handle)
    {
        Debug.Assert(handle is not 0);
        
        GCHandle<object>.FromIntPtr(handle).Dispose();
    }

    public static unsafe void Free(nint handle)
    {
        Debug.Assert(handle is not 0);
        
        NativeMemory.Free(handle.ToPointer());
    }

    public static unsafe void FreeAligned(nint handle)
    {
        Debug.Assert(handle is not 0);
        
        NativeMemory.AlignedFree(handle.ToPointer());
    }
}

internal unsafe interface ICleaner<TConvention> : ICleaner
    where TConvention : class, new()
{
    public new static abstract void* FreeRef { get; }
    
    public new static abstract void* Free { get; }
    
    public new static abstract void* FreeAligned { get; }
}

file readonly unsafe ref struct OpaqueValueCleaner : ICleaner<CallConvCdecl>, ICleaner<CallConvStdcall>, ICleaner<CallConvAuto>
{
    static void* ICleaner<CallConvCdecl>.FreeRef
    {
        get
        {
            return (delegate*unmanaged[Cdecl]<nint, void>)&FreeCore;
            
            [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
            static void FreeCore(nint handle) => ICleaner.FreeRef(handle);
        }
    }

    static void* ICleaner<CallConvCdecl>.Free
    {
        get
        {
            return (delegate*unmanaged[Cdecl]<nint, void>)&FreeCore;
            
            [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
            static void FreeCore(nint handle) => ICleaner.Free(handle);
        }
    }

    static void* ICleaner<CallConvCdecl>.FreeAligned
    {
        get
        {
            return (delegate*unmanaged[Cdecl]<nint, void>)&FreeCore;
            
            [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
            static void FreeCore(nint handle) => ICleaner.FreeAligned(handle);
        }
    }
    
    static void* ICleaner<CallConvStdcall>.FreeRef
    {
        get
        {
            return (delegate*unmanaged[Stdcall]<nint, void>)&FreeCore;
            
            [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
            static void FreeCore(nint handle) => ICleaner.FreeRef(handle);
        }
    }

    static void* ICleaner<CallConvStdcall>.Free
    {
        get
        {
            return (delegate*unmanaged[Stdcall]<nint, void>)&FreeCore;
            
            [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
            static void FreeCore(nint handle) => ICleaner.Free(handle);
        }
    }

    static void* ICleaner<CallConvStdcall>.FreeAligned
    {
        get
        {
            return (delegate*unmanaged[Stdcall]<nint, void>)&FreeCore;
            
            [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
            static void FreeCore(nint handle) => ICleaner.FreeAligned(handle);
        }
    }
    
    static void* ICleaner<CallConvAuto>.FreeRef
    {
        get
        {
            return (delegate*unmanaged<nint, void>)&FreeCore;
            
            [UnmanagedCallersOnly]
            static void FreeCore(nint handle) => ICleaner.FreeRef(handle);
        }
    }

    static void* ICleaner<CallConvAuto>.Free
    {
        get
        {
            return (delegate*unmanaged<nint, void>)&FreeCore;
            
            [UnmanagedCallersOnly]
            static void FreeCore(nint handle) => ICleaner.Free(handle);
        }
    }

    static void* ICleaner<CallConvAuto>.FreeAligned
    {
        get
        {
            return (delegate*unmanaged<nint, void>)&FreeCore;

            [UnmanagedCallersOnly]
            static void FreeCore(nint handle) => ICleaner.FreeAligned(handle);
        }
    }
}

file sealed class CallConvAuto;