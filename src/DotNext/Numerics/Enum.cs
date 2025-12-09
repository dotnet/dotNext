using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Numerics;

/// <summary>
/// Treats enum as a binary integer.
/// </summary>
/// <param name="value">The value to wrap.</param>
/// <typeparam name="T">The type of the enum.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly partial struct Enum<T>(T value) : IBinaryInteger<Enum<T>>
    where T : struct, Enum
{
    private static readonly TypeCode UnderlyingType = Type.GetTypeCode(typeof(T));

    private readonly T value = value;
    
    [Conditional("DEBUG")]
    private static void AssertUnderlyingType<TValue>()
        where TValue : unmanaged
        => Debug.Assert(UnderlyingType == Type.GetTypeCode(typeof(TValue)));

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? other) => other switch
    {
        T x => Equals(x),
        Enum<T> x => Equals(x),
        _ => false,
    };

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(value),
            TypeCode.SByte => ConstrainedCall<sbyte>(value),
            TypeCode.UInt16 => ConstrainedCall<ushort>(value),
            TypeCode.Int16 => ConstrainedCall<short>(value),
            TypeCode.UInt32 => ConstrainedCall<uint>(value),
            TypeCode.Int32 => ConstrainedCall<int>(value),
            TypeCode.UInt64 => ConstrainedCall<ulong>(value),
            TypeCode.Int64 => ConstrainedCall<long>(value),
            _ => value.GetHashCode(),
        };
        
        static int ConstrainedCall<TValue>(T value)
            where TValue : unmanaged
        {
            AssertUnderlyingType<TValue>();
            
            return Unsafe.As<T, TValue>(ref value).GetHashCode();
        }
    }

    /// <inheritdoc/>
    public override string ToString() => value.ToString();

    /// <summary>
    /// Converts the enum container to its underlying value.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The converted value.</returns>
    public static implicit operator T(Enum<T> value) => value.value;

    /// <summary>
    /// Converts the enum value to the underlying binary integer.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    /// <returns>The binary integer that represents the enum value.</returns>
    public static explicit operator Enum<T>(T value) => new(value);
}

internal static partial class EnumHelpers
{
    internal static bool Fail<T>(out T? result)
    {
        result = default;
        return false;
    }
}