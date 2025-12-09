using System.Numerics;
using System.Runtime.CompilerServices;

namespace DotNext;

partial struct Enum<T> : IEquatable<T>
{
    /// <inheritdoc/>
    int IComparable.CompareTo(object? obj) => value.CompareTo(obj);

    /// <inheritdoc/>
    public int CompareTo(Enum<T> other) => Comparer<T>.Default.Compare(value, other.value);

    /// <inheritdoc/>
    public static bool operator <(Enum<T> left, Enum<T> right)
        => left.Compare<LessThan>(right.value);

    /// <inheritdoc/>
    public static bool operator >(Enum<T> left, Enum<T> right)
        => left.Compare<GreaterThan>(right.value);

    /// <inheritdoc/>
    public static bool operator <=(Enum<T> left, Enum<T> right)
        => left.Compare<LessThanOrEqual>(right.value);

    /// <inheritdoc/>
    public static bool operator >=(Enum<T> left, Enum<T> right)
        => left.Compare<GreaterThanOrEqual>(right.value);

    /// <inheritdoc/>
    public static bool operator ==(Enum<T> left, Enum<T> right)
        => left.Compare<AreEqual>(right.value);

    /// <inheritdoc/>
    public static bool operator !=(Enum<T> left, Enum<T> right)
        => left.Compare<AreNotEqual>(right.value);

    /// <inheritdoc/>
    public bool Equals(Enum<T> other) => Equals(other.value);

    /// <inheritdoc/>
    public bool Equals(T other) => Compare<AreEqual>(other);

    private bool Compare<TComparer>(T right)
        where TComparer : EnumHelpers.IComparer, allows ref struct
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(value, right),
            TypeCode.SByte => ConstrainedCall<sbyte>(value, right),
            TypeCode.UInt16 => ConstrainedCall<ushort>(value, right),
            TypeCode.Int16 => ConstrainedCall<short>(value, right),
            TypeCode.UInt32 => ConstrainedCall<uint>(value, right),
            TypeCode.Int32 => ConstrainedCall<int>(value, right),
            TypeCode.UInt64 => ConstrainedCall<ulong>(value, right),
            TypeCode.Int64 => ConstrainedCall<long>(value, right),
            _ => false,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool ConstrainedCall<TValue>(T left, T right)
            where TValue : unmanaged, IComparisonOperators<TValue, TValue, bool>
        {
            AssertUnderlyingType<TValue>();

            return TComparer.Compare(Unsafe.BitCast<T, TValue>(left), Unsafe.BitCast<T, TValue>(right));
        }
    }
}

partial class EnumHelpers
{
    internal interface IComparer
    {
        static abstract bool Compare<TValue>(TValue left, TValue right)
            where TValue : unmanaged, IComparisonOperators<TValue, TValue, bool>;
    }
}

file readonly ref struct LessThan : EnumHelpers.IComparer
{
    static bool EnumHelpers.IComparer.Compare<TValue>(TValue left, TValue right)
        => left < right;
}

file readonly ref struct GreaterThan : EnumHelpers.IComparer
{
    static bool EnumHelpers.IComparer.Compare<TValue>(TValue left, TValue right)
        => left > right;
}

file readonly ref struct LessThanOrEqual : EnumHelpers.IComparer
{
    static bool EnumHelpers.IComparer.Compare<TValue>(TValue left, TValue right)
        => left <= right;
}

file readonly ref struct GreaterThanOrEqual : EnumHelpers.IComparer
{
    static bool EnumHelpers.IComparer.Compare<TValue>(TValue left, TValue right)
        => left >= right;
}

file readonly ref struct AreEqual : EnumHelpers.IComparer
{
    static bool EnumHelpers.IComparer.Compare<TValue>(TValue left, TValue right)
        => left == right;
}

file readonly ref struct AreNotEqual : EnumHelpers.IComparer
{
    static bool EnumHelpers.IComparer.Compare<TValue>(TValue left, TValue right)
        => left != right;
}