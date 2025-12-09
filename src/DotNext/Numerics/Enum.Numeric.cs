using System.Numerics;
using System.Runtime.CompilerServices;

namespace DotNext.Numerics;

partial struct Enum<T>
{
    /// <inheritdoc/>
    static Enum<T> IAdditiveIdentity<Enum<T>, Enum<T>>.AdditiveIdentity => default;

    /// <inheritdoc/>
    static Enum<T> IMultiplicativeIdentity<Enum<T>, Enum<T>>.MultiplicativeIdentity => One;

    /// <inheritdoc/>
    static Enum<T> INumberBase<Enum<T>>.Abs(Enum<T> value)
        => value.UnaryOperation<Abs>();

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.IsCanonical(Enum<T> value) => true;

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.IsComplexNumber(Enum<T> value) => false;

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.IsEvenInteger(Enum<T> value) => value.IsEven;

    /// <summary>
    /// Gets a value indicating that the underlying value is even.
    /// </summary>
    public bool IsEven => Check<IsEvenCheck>();

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.IsFinite(Enum<T> value) => true;

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.IsImaginaryNumber(Enum<T> value) => false;

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.IsInfinity(Enum<T> value) => false;

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.IsInteger(Enum<T> value) => true;

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.IsNaN(Enum<T> value) => false;

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.IsNegative(Enum<T> value) => value.IsNegative;

    /// <summary>
    /// Gets a value indicating that the underlying value is negative.
    /// </summary>
    public bool IsNegative => Check<IsNegativeCheck>();

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.IsNegativeInfinity(Enum<T> value) => false;

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.IsNormal(Enum<T> value) => value.IsNotZero;

    /// <summary>
    /// Gets a value indicating that the underlying value is not zero.
    /// </summary>
    public bool IsNotZero => Check<IsNormalCheck>();

    static bool INumberBase<Enum<T>>.IsOddInteger(Enum<T> value) => value.IsOdd;

    /// <summary>
    /// Gets a value indicating that the underlying value is odd.
    /// </summary>
    public bool IsOdd => Check<IsOddCheck>();

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.IsPositive(Enum<T> value) => value.IsPositive;

    /// <summary>
    /// Gets a value indicating that the underlying value is positive.
    /// </summary>
    public bool IsPositive => Check<IsPositiveCheck>();

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.IsPositiveInfinity(Enum<T> value) => false;

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.IsRealNumber(Enum<T> value) => true;

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.IsSubnormal(Enum<T> value) => false;

    private bool Check<TChecker>()
        where TChecker : EnumHelpers.IChecker, allows ref struct
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
            _ => false,
        };

        static bool ConstrainedCall<TValue>(T value)
            where TValue : unmanaged, IBinaryInteger<TValue>
        {
            AssertUnderlyingType<TValue>();

            return TChecker.Check(Unsafe.BitCast<T, TValue>(value));
        }
    }

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.IsZero(Enum<T> value)
        => value.Check<IsZeroCheck>();

    /// <inheritdoc/>
    static Enum<T> INumberBase<Enum<T>>.MaxMagnitude(Enum<T> x, Enum<T> y)
        => x.BinaryOperation<MaxMagnitude>(y);

    /// <inheritdoc/>
    static Enum<T> INumberBase<Enum<T>>.MaxMagnitudeNumber(Enum<T> x, Enum<T> y)
        => x.BinaryOperation<MaxMagnitude>(y);

    /// <inheritdoc/>
    public static Enum<T> Max(Enum<T> x, Enum<T> y)
        => x.BinaryOperation<MaxValue>(y);

    /// <inheritdoc/>
    static Enum<T> INumberBase<Enum<T>>.MinMagnitude(Enum<T> x, Enum<T> y)
        => x.BinaryOperation<MinMagnitude>(y);

    /// <inheritdoc/>
    static Enum<T> INumberBase<Enum<T>>.MinMagnitudeNumber(Enum<T> x, Enum<T> y)
        => x.BinaryOperation<MinMagnitude>(y);

    /// <inheritdoc/>
    public static Enum<T> Min(Enum<T> x, Enum<T> y)
        => x.BinaryOperation<MinValue>(y);

    /// <inheritdoc/>
    static Enum<T> INumberBase<Enum<T>>.One => One;

    private static Enum<T> One
    {
        get
        {
            return UnderlyingType switch
            {
                TypeCode.Byte => ConstrainedCall<byte>(),
                TypeCode.SByte => ConstrainedCall<sbyte>(),
                TypeCode.Int16 => ConstrainedCall<short>(),
                TypeCode.UInt16 => ConstrainedCall<ushort>(),
                TypeCode.Int32 => ConstrainedCall<int>(),
                TypeCode.UInt32 => ConstrainedCall<uint>(),
                TypeCode.Int64 => ConstrainedCall<long>(),
                TypeCode.UInt64 => ConstrainedCall<ulong>(),
                _ => default,
            };

            static Enum<T> ConstrainedCall<TValue>()
                where TValue : unmanaged, INumberBase<TValue>
            {
                AssertUnderlyingType<TValue>();

                return Unsafe.BitCast<TValue, Enum<T>>(TValue.One);
            }
        }
    }

    /// <inheritdoc/>
    static int INumberBase<Enum<T>>.Radix => 2;

    /// <inheritdoc/>
    static Enum<T> INumberBase<Enum<T>>.Zero => default;

    /// <inheritdoc/>
    static bool IBinaryNumber<Enum<T>>.IsPow2(Enum<T> value) => value.IsPow2;

    /// <summary>
    /// Gets a value indicating that the underlying value is a power of two.
    /// </summary>
    public bool IsPow2 => Check<IsPow2Check>();

    /// <inheritdoc/>
    static Enum<T> IBinaryNumber<Enum<T>>.Log2(Enum<T> value)
        => value.UnaryOperation<Log2>();
    
    static Enum<T> IBinaryNumber<Enum<T>>.AllBitsSet
    {
        get
        {
            return UnderlyingType switch
            {
                TypeCode.Byte => ConstrainedCall<byte>(),
                TypeCode.SByte => ConstrainedCall<sbyte>(),
                TypeCode.Int16 => ConstrainedCall<short>(),
                TypeCode.UInt16 => ConstrainedCall<ushort>(),
                TypeCode.Int32 => ConstrainedCall<int>(),
                TypeCode.UInt32 => ConstrainedCall<uint>(),
                TypeCode.Int64 => ConstrainedCall<long>(),
                TypeCode.UInt64 => ConstrainedCall<ulong>(),
                _ => default,
            };

            static Enum<T> ConstrainedCall<TValue>()
                where TValue : unmanaged, IBinaryNumber<TValue>
            {
                AssertUnderlyingType<TValue>();

                return Unsafe.BitCast<TValue, Enum<T>>(TValue.AllBitsSet);
            }
        }
    }

    /// <inheritdoc/>
    public int GetShortestBitLength()
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(value),
            TypeCode.SByte => ConstrainedCall<sbyte>(value),
            TypeCode.Int16 => ConstrainedCall<short>(value),
            TypeCode.UInt16 => ConstrainedCall<ushort>(value),
            TypeCode.Int32 => ConstrainedCall<int>(value),
            TypeCode.UInt32 => ConstrainedCall<uint>(value),
            TypeCode.Int64 => ConstrainedCall<long>(value),
            TypeCode.UInt64 => ConstrainedCall<ulong>(value),
            _ => 0,
        };

        static int ConstrainedCall<TValue>(T value)
            where TValue : unmanaged, IBinaryInteger<TValue>
        {
            AssertUnderlyingType<TValue>();

            return Unsafe.BitCast<T, TValue>(value).GetShortestBitLength();
        }
    }

    /// <inheritdoc/>
    static Enum<T> IBinaryInteger<Enum<T>>.PopCount(Enum<T> value)
        => value.UnaryOperation<PopCount>();
}

partial class EnumHelpers
{
    internal interface IChecker
    {
        static abstract bool Check<TValue>(TValue value)
            where TValue : unmanaged, IBinaryInteger<TValue>;
    }
}

file readonly ref struct PopCount : EnumHelpers.IUnaryOperation
{
    static TValue EnumHelpers.IUnaryOperation.Invoke<TValue>(TValue operand)
        => TValue.PopCount(operand);
}

file readonly ref struct Log2 : EnumHelpers.IUnaryOperation
{
    static TValue EnumHelpers.IUnaryOperation.Invoke<TValue>(TValue operand)
        => TValue.Log2(operand);
}

file readonly ref struct Abs : EnumHelpers.IUnaryOperation
{
    static TValue EnumHelpers.IUnaryOperation.Invoke<TValue>(TValue operand)
        => TValue.Abs(operand);
}

file readonly ref struct MaxMagnitude : EnumHelpers.IBinaryOperation
{
    static TValue EnumHelpers.IBinaryOperation.Invoke<TValue>(TValue left, TValue right)
        => TValue.MaxMagnitude(left, right);
}

file readonly ref struct MinMagnitude : EnumHelpers.IBinaryOperation
{
    static TValue EnumHelpers.IBinaryOperation.Invoke<TValue>(TValue left, TValue right)
        => TValue.MinMagnitude(left, right);
}

file readonly ref struct MaxValue : EnumHelpers.IBinaryOperation
{
    static TValue EnumHelpers.IBinaryOperation.Invoke<TValue>(TValue left, TValue right)
        => TValue.Max(left, right);
}

file readonly ref struct MinValue : EnumHelpers.IBinaryOperation
{
    static TValue EnumHelpers.IBinaryOperation.Invoke<TValue>(TValue left, TValue right)
        => TValue.Min(left, right);
}

file readonly ref struct IsOddCheck : EnumHelpers.IChecker
{
    static bool EnumHelpers.IChecker.Check<TValue>(TValue value)
        => TValue.IsOddInteger(value);
}

file readonly ref struct IsEvenCheck : EnumHelpers.IChecker
{
    static bool EnumHelpers.IChecker.Check<TValue>(TValue value)
        => TValue.IsEvenInteger(value);
}

file readonly ref struct IsNegativeCheck : EnumHelpers.IChecker
{
    static bool EnumHelpers.IChecker.Check<TValue>(TValue value)
        => TValue.IsNegative(value);
}

file readonly ref struct IsPositiveCheck : EnumHelpers.IChecker
{
    static bool EnumHelpers.IChecker.Check<TValue>(TValue value)
        => TValue.IsNegative(value);
}

file readonly ref struct IsNormalCheck : EnumHelpers.IChecker
{
    static bool EnumHelpers.IChecker.Check<TValue>(TValue value)
        => TValue.IsNormal(value);
}

file readonly ref struct IsZeroCheck : EnumHelpers.IChecker
{
    static bool EnumHelpers.IChecker.Check<TValue>(TValue value)
        => TValue.IsZero(value);
}

file readonly ref struct IsPow2Check : EnumHelpers.IChecker
{
    static bool EnumHelpers.IChecker.Check<TValue>(TValue value)
        => TValue.IsPow2(value);
}