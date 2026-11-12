using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Reflection;

/// <summary>
/// Describes binary representation of the nullable data type in the runtime.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly ref struct NullableInfo
{
    public readonly Type ValueType;
    public readonly ref bool HasValue;
    public readonly ref byte Value;

    private NullableInfo(ref byte location, int valueOffset, Type valueType)
    {
        HasValue = ref Unsafe.As<byte, bool>(ref location);
        Value = ref Unsafe.Add(ref location, valueOffset);
        ValueType = valueType;
    }
    
    public static bool TryCreate(ref byte location, Type type, out NullableInfo info)
    {
        if (Nullable.GetUnderlyingType(type) is { } valueType)
        {
            var offset = RuntimeHelpers.SizeOf(type.TypeHandle) - RuntimeHelpers.SizeOf(valueType.TypeHandle);
            info = new(ref location, offset, valueType);
            return true;
        }

        info = default;
        return false;
    }
}