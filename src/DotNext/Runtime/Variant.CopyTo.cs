using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime;

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

        var sourceRuntimeTypeHandle = RuntimeTypeHandle.FromIntPtr(sourceTypeHandle);
        var destinationRuntimeTypeHandle = RuntimeTypeHandle.FromIntPtr(destinationTypeHandle);
        return destinationTypeHandle != destination.typeInfo
               && Type.GetTypeFromHandle(sourceRuntimeTypeHandle) is { } sourceType
               && Type.GetTypeFromHandle(destinationRuntimeTypeHandle) is { } destinationType
               && destinationType.IsAssignableFrom(sourceType)
               && TryCopy(ref location,
                   sourceType,
                   sourceRuntimeTypeHandle,
                   ref destination.location,
                   destinationType,
                   destinationRuntimeTypeHandle);
    }

    private static bool TryCopy(ref byte source, Type sourceType, RuntimeTypeHandle sourceTypeHandle,
        ref byte destination, Type destinationType, RuntimeTypeHandle destinationTypeHandle)
    {
        Debug.Assert(destinationType.IsAssignableFrom(sourceType));

        switch (sourceType.IsValueType, destinationType.IsValueType)
        {
            case (true, false) when !sourceType.IsByRefLike:
                Unsafe.As<byte, object?>(ref destination) = RuntimeHelpers.Box(ref source, sourceTypeHandle);
                return true;
            case (true, true) when sourceTypeHandle.Value == destinationTypeHandle.Value:
                Buffer.CopyValueType(ref destination, ref source, sourceTypeHandle);
                return true;
            case (false, false):
                if (sourceType.IsPointer || sourceType.IsFunctionPointer)
                {
                    Unsafe.As<byte, nuint>(ref destination) = Unsafe.As<byte, nuint>(ref source);
                }
                else
                {
                    Unsafe.As<byte, object?>(ref destination) = Unsafe.As<byte, object?>(ref source);
                }

                return true;
            default:
                return false;
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
                Buffer.ClearValueType(ref location, runtimeHandle);
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
        public static void CopyValueType(ref byte destination, ref byte source, RuntimeTypeHandle handle)
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

        public static void ClearValueType(ref byte location, RuntimeTypeHandle handle)
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