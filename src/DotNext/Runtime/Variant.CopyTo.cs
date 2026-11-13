using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime;

using CompilerServices;
using Reflection;

partial struct Variant
{
    /// <summary>
    /// Copies the value from the current location to the destination.
    /// </summary>
    /// <param name="destination">The destination location.</param>
    /// <returns><see langword="true"/> if both memory locations of compatible types; otherwise, <see langword="false"/>.</returns>
    public bool TryCopyTo(scoped Variant destination)
    {
        var sourceTypeHandle = TypeHandle;
        var destinationTypeHandle = destination.TypeHandle;

        if (Unsafe.AreSame(in location, in destination.location))
            return sourceTypeHandle == destinationTypeHandle;

        if (destinationTypeHandle == destination.typeInfo)
        {
            // fast path: immutable or empty destination cannot be modified
            return false;
        }

        var sourceRuntimeTypeHandle = RuntimeTypeHandle.FromIntPtr(sourceTypeHandle);
        if (sourceTypeHandle != destinationTypeHandle)
        {
            // slow path: type conversion
            return TryCopySlow(ref location,
                sourceRuntimeTypeHandle,
                ref destination.location,
                RuntimeTypeHandle.FromIntPtr(destinationTypeHandle));
        }

        // fast path: the types are equal. SizeOf returns the pointer size for reference and pointer types,
        // so the copy with GC barrier handles all kinds of types
        Buffer.Copy(ref destination.location, ref location, sourceRuntimeTypeHandle);
        return true;

        static bool TryCopySlow(ref byte source, RuntimeTypeHandle sourceTypeHandle,
            ref byte destination, RuntimeTypeHandle destinationTypeHandle)
        {
            if (Type.GetTypeFromHandle(sourceTypeHandle) is { } sourceType
                && Type.GetTypeFromHandle(destinationTypeHandle) is { } destinationType
                && destinationType.IsAssignableFrom(sourceType))
            {
                switch (sourceType.IsValueType, destinationType.IsValueType)
                {
                    case (true, false) when !sourceType.IsByRefLike:
                        Unsafe.As<byte, object?>(ref destination) = RuntimeHelpers.Box(ref source, sourceTypeHandle);
                        return true;
                    // the equal types are handled by the fast path, so only Nullable<T> remains
                    case (true, true) when NullableInfo.TryCreate(ref destination, destinationType, out var nullableInfo):
                        Buffer.Copy(ref nullableInfo.Value, ref source, sourceTypeHandle);
                        nullableInfo.HasValue = true;
                        return true;
                    // pointers are not assignable to other types, so they are handled by the fast path
                    case (false, false):
                        Debug.Assert(sourceType is { IsPointer: false, IsFunctionPointer: false });

                        Unsafe.As<byte, object?>(ref destination) = Unsafe.As<byte, object?>(ref source);
                        return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Tries to replace the referenced value.
    /// </summary>
    /// <remarks>
    /// The accepted <paramref name="value"/> depends on <see cref="TargetType"/>:
    /// <list type="bullet">
    /// <item>For a value type, the boxed value of exactly the same type.</item>
    /// <item>For <see cref="Nullable{T}"/>, the boxed value of the underlying type, or <see langword="null"/>.</item>
    /// <item>For a reference type, any compatible object, or <see langword="null"/>.</item>
    /// <item>For an unmanaged pointer type, the boxed pointer as <see cref="Pointer"/>.
    /// The type of the boxed pointer is not checked against <see cref="TargetType"/>.</item>
    /// <item>For a function pointer type, the boxed <see cref="nint"/>.</item>
    /// </list>
    /// Pointers are boxed the same way as <see cref="ToObject()"/> does, including zero pointers.
    /// By-ref-like types are not supported.
    /// </remarks>
    /// <param name="value">The value to set.</param>
    /// <returns>
    /// <see langword="true"/> if the value is replaced successfully;
    /// <see langword="false"/> if the current value is immutable or empty, or <paramref name="value"/> is of incompatible type.
    /// </returns>
    public bool TrySetValue(object? value)
    {
        var destinationTypeHandle = TypeHandle;
        RuntimeTypeHandle sourceTypeHandle;
        if (destinationTypeHandle == typeInfo)
        {
            // fast path: immutable reference cannot be modified
            return false;
        }
        
        if (value is null || (sourceTypeHandle = value.GetType().TypeHandle).Value != destinationTypeHandle)
        {
            // slow path: type mismatch
            return TrySetValueSlow(value, ref location, RuntimeTypeHandle.FromIntPtr(destinationTypeHandle));
        }
        
        if (value is ValueType)
        {
            // fast path: the boxed value is of the target type exactly
            Buffer.Copy(ref location, ref Unsafe.GetRawData(value), sourceTypeHandle);
        }
        else
        {
            // fast path: the value is of the target type exactly
            Unsafe.As<byte, object?>(ref location) = value;
        }

        return true;

        static bool TrySetValueSlow(object? source, ref byte destination, RuntimeTypeHandle destinationTypeHandle)
        {
            switch (Type.GetTypeFromHandle(destinationTypeHandle))
            {
                case null or { IsByRefLike: true }:
                    goto default;
                // the exact type is handled by the fast path, so only Nullable<T> remains
                case { IsValueType: true } destinationType:
                    if (!NullableInfo.TryCreate(ref destination, destinationType, out var nullableInfo))
                        goto default;

                    if (source is null)
                    {
                        Buffer.Clear(ref destination, destinationTypeHandle);
                    }
                    else if (source.GetType() == nullableInfo.ValueType)
                    {
                        Buffer.Copy(ref nullableInfo.Value, ref Unsafe.GetRawData(source), nullableInfo.ValueType.TypeHandle);
                        nullableInfo.HasValue = true;
                    }
                    else
                    {
                        goto default;
                    }

                    break;
                case { IsPointer: true }:
                    if (source is not Pointer)
                        goto default;

                    unsafe
                    {
                        Unsafe.As<byte, nuint>(ref destination) = new(Pointer.Unbox(source));
                    }

                    break;
                case { IsFunctionPointer: true }:
                    if (source is not nint)
                        goto default;

                    Unsafe.As<byte, nint>(ref destination) = (nint)source;
                    break;
                case { } destinationType when source is null || destinationType.IsAssignableFrom(source.GetType()):
                    Unsafe.As<byte, object?>(ref destination) = source;
                    break;
                default:
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Resets the referenced value to the default value of type <see cref="TargetType"/>.
    /// </summary>
    /// <remarks>
    /// Reference types are set to <see langword="null"/>, value types are zeroed.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The current value is immutable or empty.</exception>
    public void Clear()
    {
        var handle = TypeHandle;
        if (handle == typeInfo)
            InvalidOperationException.Throw();

        var runtimeHandle = RuntimeTypeHandle.FromIntPtr(handle);
        switch (Type.GetTypeFromHandle(runtimeHandle))
        {
            case { IsValueType: true }:
                Buffer.Clear(ref location, runtimeHandle);
                break;
            case { IsPointer: true } or { IsFunctionPointer: true }:
                Unsafe.As<byte, nuint>(ref location) = 0U;
                break;
            default:
                Unsafe.As<byte, object?>(ref location) = null;
                break;
        }
    }
}

file static class BufferExtensions
{
    extension(Buffer)
    {
        public static void Copy(ref byte destination, ref byte source, RuntimeTypeHandle handle)
        {
            var size = RuntimeHelpers.SizeOf(handle);
            if (size % nuint.Size is 0)
            {
                CopyWithGcBarrier(null, ref destination, ref source, (uint)size);
            }
            else
            {
                Unsafe.CopyBlockUnaligned(ref destination, ref source, (uint)size);
            }
        }

        public static void Clear(ref byte location, RuntimeTypeHandle handle)
        {
            var size = RuntimeHelpers.SizeOf(handle);
            if (size % nuint.Size is 0)
            {
                ClearWithGcBarrier(null,
                    ref Unsafe.As<byte, nint>(ref location),
                    (nuint)(size / nint.Size));
            }
            else
            {
                Unsafe.InitBlockUnaligned(ref location, 0, (uint)size);
            }
        }
    }
    
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "BulkMoveWithWriteBarrier")]
    private static extern void CopyWithGcBarrier(
        [UnsafeAccessorType("System.Buffer, System.Private.CoreLib")]
        object? buffer,
        ref byte destination, ref byte source, nuint byteCount);
    
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "ClearWithReferences")]
    private static extern void ClearWithGcBarrier(
        [UnsafeAccessorType("System.SpanHelpers, System.Private.CoreLib")]
        object? helpers,
        ref nint ip, nuint pointerSizeLength);
}