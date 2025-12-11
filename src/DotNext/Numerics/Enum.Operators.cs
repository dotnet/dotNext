using System.Numerics;
using System.Runtime.CompilerServices;

namespace DotNext.Numerics;

partial struct Enum<T>
{
    /// <inheritdoc/>
    public static Enum<T> operator +(Enum<T> left, Enum<T> right)
        => left.BinaryOperation<Addition>(right);
    
    /// <inheritdoc/>
    public static Enum<T> operator checked +(Enum<T> left, Enum<T> right)
        => left.BinaryOperation<AdditionChecked>(right);

    /// <inheritdoc/>
    public static Enum<T> operator &(Enum<T> left, Enum<T> right)
        => left.BinaryOperation<And>(right);

    /// <inheritdoc/>
    public static Enum<T> operator |(Enum<T> left, Enum<T> right)
        => left.BinaryOperation<Or>(right);

    /// <inheritdoc/>
    public static Enum<T> operator ^(Enum<T> left, Enum<T> right)
        => left.BinaryOperation<Xor>(right);

    /// <inheritdoc/>
    public static Enum<T> operator ~(Enum<T> value)
        => value.UnaryOperation<OneComplement>();

    /// <inheritdoc/>
    public static Enum<T> operator --(Enum<T> value)
        => value.UnaryOperation<Decrement>();
    
    /// <inheritdoc/>
    public static Enum<T> operator checked --(Enum<T> value)
        => value.UnaryOperation<DecrementChecked>();

    /// <inheritdoc/>
    public static Enum<T> operator /(Enum<T> left, Enum<T> right)
        => left.BinaryOperation<Division>(right);
    
    /// <inheritdoc/>
    public static Enum<T> operator checked /(Enum<T> left, Enum<T> right)
        => left.BinaryOperation<DivisionChecked>(right);

    /// <inheritdoc/>
    public static Enum<T> operator ++(Enum<T> value)
        => value.UnaryOperation<Increment>();
    
    /// <inheritdoc/>
    public static Enum<T> operator checked ++(Enum<T> value)
        => value.UnaryOperation<IncrementChecked>();

    /// <inheritdoc/>
    public static Enum<T> operator %(Enum<T> left, Enum<T> right)
        => left.BinaryOperation<Modulo>(right);

    /// <inheritdoc/>
    public static Enum<T> operator *(Enum<T> left, Enum<T> right)
        => left.BinaryOperation<Multiplication>(right);

    /// <inheritdoc/>
    public static Enum<T> operator checked *(Enum<T> left, Enum<T> right)
        => left.BinaryOperation<MultiplicationChecked>(right);

    /// <inheritdoc/>
    public static Enum<T> operator -(Enum<T> left, Enum<T> right)
        => left.BinaryOperation<Subtraction>(right);

    /// <inheritdoc/>
    public static Enum<T> operator checked -(Enum<T> left, Enum<T> right)
        => left.BinaryOperation<SubtractionChecked>(right);

    /// <inheritdoc/>
    public static Enum<T> operator -(Enum<T> value)
        => value.UnaryOperation<UnaryMinus>();

    /// <inheritdoc/>
    public static Enum<T> operator checked -(Enum<T> value)
        => value.UnaryOperation<UnaryMinusChecked>();

    /// <inheritdoc/>
    public static Enum<T> operator +(Enum<T> value)
        => value.UnaryOperation<UnaryPlus>();

    /// <inheritdoc/>
    public static Enum<T> operator <<(Enum<T> value, int shiftAmount)
        => BinaryOperation<LeftShift, int>(value, shiftAmount);

    /// <inheritdoc/>
    public static Enum<T> operator >> (Enum<T> value, int shiftAmount)
        => BinaryOperation<RightShift, int>(value, shiftAmount);

    /// <inheritdoc/>
    public static Enum<T> operator >>> (Enum<T> value, int shiftAmount)
        => BinaryOperation<RightShiftUnsigned, int>(value, shiftAmount);

    private Enum<T> BinaryOperation<TOperation>(T right)
        where TOperation : EnumHelpers.IBinaryOperation, allows ref struct
    {
        return new(UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(value, right),
            TypeCode.SByte => ConstrainedCall<sbyte>(value, right),
            TypeCode.UInt16 => ConstrainedCall<ushort>(value, right),
            TypeCode.Int16 => ConstrainedCall<short>(value, right),
            TypeCode.UInt32 => ConstrainedCall<uint>(value, right),
            TypeCode.Int32 => ConstrainedCall<int>(value, right),
            TypeCode.UInt64 => ConstrainedCall<ulong>(value, right),
            TypeCode.Int64 => ConstrainedCall<long>(value, right),
            _ => default,
        });
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static T ConstrainedCall<TValue>(T left, T right)
            where TValue : unmanaged, IBinaryInteger<TValue>
        {
            AssertUnderlyingType<TValue>();

            return Unsafe.BitCast<TValue, T>(TOperation.Invoke(
                Unsafe.BitCast<T, TValue>(left),
                Unsafe.BitCast<T, TValue>(right)));
        }
    }

    private Enum<T> UnaryOperation<TOperation>()
        where TOperation : EnumHelpers.IUnaryOperation, allows ref struct
    {
        return new(UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(value),
            TypeCode.SByte => ConstrainedCall<sbyte>(value),
            TypeCode.UInt16 => ConstrainedCall<ushort>(value),
            TypeCode.Int16 => ConstrainedCall<short>(value),
            TypeCode.UInt32 => ConstrainedCall<uint>(value),
            TypeCode.Int32 => ConstrainedCall<int>(value),
            TypeCode.UInt64 => ConstrainedCall<ulong>(value),
            TypeCode.Int64 => ConstrainedCall<long>(value),
            _ => value,
        });
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static T ConstrainedCall<TValue>(T operand)
            where TValue : unmanaged, IBinaryInteger<TValue>
        {
            AssertUnderlyingType<TValue>();

            return Unsafe.BitCast<TValue, T>(
                TOperation.Invoke(Unsafe.BitCast<T, TValue>(operand)));
        }
    }

    private static Enum<T> BinaryOperation<TOperation, TRight>(Enum<T> operand, TRight right)
        where TOperation : EnumHelpers.IBinaryOperation<TRight>, allows ref struct
        where TRight : allows ref struct
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(operand, right),
            TypeCode.SByte => ConstrainedCall<sbyte>(operand, right),
            TypeCode.UInt16 => ConstrainedCall<ushort>(operand, right),
            TypeCode.Int16 => ConstrainedCall<short>(operand, right),
            TypeCode.UInt32 => ConstrainedCall<uint>(operand, right),
            TypeCode.Int32 => ConstrainedCall<int>(operand, right),
            TypeCode.UInt64 => ConstrainedCall<ulong>(operand, right),
            TypeCode.Int64 => ConstrainedCall<long>(operand, right),
            _ => operand,
        };
        
        static Enum<T> ConstrainedCall<TValue>(Enum<T> operand, TRight right)
            where TValue : unmanaged, IBinaryInteger<TValue>
        {
            AssertUnderlyingType<TValue>();

            return Unsafe.BitCast<TValue, Enum<T>>(
                TOperation.Invoke(Unsafe.BitCast<Enum<T>, TValue>(operand), right));
        }
    }
}

partial class EnumHelpers
{
    internal interface IBinaryOperation
    {
        static abstract TValue Invoke<TValue>(TValue left, TValue right)
            where TValue : unmanaged, IBinaryInteger<TValue>;
    }
    
    internal interface IUnaryOperation
    {
        static abstract TValue Invoke<TValue>(TValue operand)
            where TValue : unmanaged, IBinaryInteger<TValue>;
    }
    
    internal interface IBinaryOperation<in TRight>
        where TRight : allows ref struct
    {
        static abstract TValue Invoke<TValue>(TValue left, TRight right)
            where TValue : unmanaged, IBinaryInteger<TValue>;
    }
}

file readonly ref struct Addition : EnumHelpers.IBinaryOperation
{
    static TValue EnumHelpers.IBinaryOperation.Invoke<TValue>(TValue left, TValue right)
        => left + right;
}

file readonly ref struct AdditionChecked : EnumHelpers.IBinaryOperation
{
    static TValue EnumHelpers.IBinaryOperation.Invoke<TValue>(TValue left, TValue right)
        => checked(left + right);
}

file readonly ref struct Subtraction : EnumHelpers.IBinaryOperation
{
    static TValue EnumHelpers.IBinaryOperation.Invoke<TValue>(TValue left, TValue right)
        => left - right;
}

file readonly ref struct SubtractionChecked : EnumHelpers.IBinaryOperation
{
    static TValue EnumHelpers.IBinaryOperation.Invoke<TValue>(TValue left, TValue right)
        => checked(left - right);
}

file readonly ref struct And : EnumHelpers.IBinaryOperation
{
    static TValue EnumHelpers.IBinaryOperation.Invoke<TValue>(TValue left, TValue right)
        => left & right;
}

file readonly ref struct Xor : EnumHelpers.IBinaryOperation
{
    static TValue EnumHelpers.IBinaryOperation.Invoke<TValue>(TValue left, TValue right)
        => left ^ right;
}

file readonly ref struct Or : EnumHelpers.IBinaryOperation
{
    static TValue EnumHelpers.IBinaryOperation.Invoke<TValue>(TValue left, TValue right)
        => left | right;
}

file readonly ref struct Multiplication : EnumHelpers.IBinaryOperation
{
    static TValue EnumHelpers.IBinaryOperation.Invoke<TValue>(TValue left, TValue right)
        => left * right;
}

file readonly ref struct MultiplicationChecked : EnumHelpers.IBinaryOperation
{
    static TValue EnumHelpers.IBinaryOperation.Invoke<TValue>(TValue left, TValue right)
        => checked(left * right);
}

file readonly ref struct Division : EnumHelpers.IBinaryOperation
{
    static TValue EnumHelpers.IBinaryOperation.Invoke<TValue>(TValue left, TValue right)
        => left / right;
}

file readonly ref struct DivisionChecked : EnumHelpers.IBinaryOperation
{
    static TValue EnumHelpers.IBinaryOperation.Invoke<TValue>(TValue left, TValue right)
        => checked(left / right);
}

file readonly ref struct Modulo : EnumHelpers.IBinaryOperation
{
    static TValue EnumHelpers.IBinaryOperation.Invoke<TValue>(TValue left, TValue right)
        => left % right;
}

file readonly ref struct OneComplement : EnumHelpers.IUnaryOperation
{
    static TValue EnumHelpers.IUnaryOperation.Invoke<TValue>(TValue operand)
        => ~operand;
}

file readonly ref struct UnaryPlus : EnumHelpers.IUnaryOperation
{
    static TValue EnumHelpers.IUnaryOperation.Invoke<TValue>(TValue operand)
        => +operand;
}

file readonly ref struct UnaryMinus : EnumHelpers.IUnaryOperation
{
    static TValue EnumHelpers.IUnaryOperation.Invoke<TValue>(TValue operand)
        => -operand;
}

file readonly ref struct UnaryMinusChecked : EnumHelpers.IUnaryOperation
{
    static TValue EnumHelpers.IUnaryOperation.Invoke<TValue>(TValue operand)
        => checked(-operand);
}

file readonly ref struct Increment : EnumHelpers.IUnaryOperation
{
    static TValue EnumHelpers.IUnaryOperation.Invoke<TValue>(TValue operand)
        => ++operand;
}

file readonly ref struct IncrementChecked : EnumHelpers.IUnaryOperation
{
    static TValue EnumHelpers.IUnaryOperation.Invoke<TValue>(TValue operand)
        => checked(++operand);
}

file readonly ref struct Decrement : EnumHelpers.IUnaryOperation
{
    static TValue EnumHelpers.IUnaryOperation.Invoke<TValue>(TValue operand)
        => --operand;
}

file readonly ref struct DecrementChecked : EnumHelpers.IUnaryOperation
{
    static TValue EnumHelpers.IUnaryOperation.Invoke<TValue>(TValue operand)
        => checked(--operand);
}

file readonly ref struct LeftShift : EnumHelpers.IBinaryOperation<int>
{
    static TValue EnumHelpers.IBinaryOperation<int>.Invoke<TValue>(TValue left, int shiftAmount)
        => left << shiftAmount;
}

file readonly ref struct RightShift : EnumHelpers.IBinaryOperation<int>
{
    static TValue EnumHelpers.IBinaryOperation<int>.Invoke<TValue>(TValue left, int shiftAmount)
        => left >> shiftAmount;
}

file readonly ref struct RightShiftUnsigned : EnumHelpers.IBinaryOperation<int>
{
    static TValue EnumHelpers.IBinaryOperation<int>.Invoke<TValue>(TValue left, int shiftAmount)
        => left >>> shiftAmount;
}