using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;

namespace DotNext
{
    /// <summary>
    /// Various extensions for value types.
    /// </summary>
    public static class ValueTypeExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string ToString<T>(T value, IFormatProvider? provider = null)
            where T : struct, IConvertible => value.ToString(provider);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string ToString<T>(T value, string format, IFormatProvider? provider = null)
            where T : struct, IFormattable => value.ToString(format, provider);

        /// <summary>
        /// Checks whether the specified value is equal to one
        /// of the specified values.
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="IEquatable{T}.Equals(T)"/>
        /// to check equality between two values.
        /// </remarks>
        /// <typeparam name="T">The type of object to compare.</typeparam>
        /// <param name="value">The value to compare with other.</param>
        /// <param name="values">Candidate objects.</param>
        /// <returns><see langword="true"/>, if <paramref name="value"/> is equal to one of <paramref name="values"/>.</returns>
        public static bool IsOneOf<T>(this T value, IEnumerable<T> values)
            where T : struct, IEquatable<T>
        {
            foreach (var v in values)
            {
                if (v.Equals(value))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether the specified value is equal to one
        /// of the specified values.
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="IEquatable{T}.Equals(T)"/>
        /// to check equality between two values.
        /// </remarks>
        /// <typeparam name="T">The type of object to compare.</typeparam>
        /// <param name="value">The value to compare with other.</param>
        /// <param name="values">Candidate objects.</param>
        /// <returns><see langword="true"/>, if <paramref name="value"/> is equal to one of <paramref name="values"/>.</returns>
        public static bool IsOneOf<T>(this T value, params T[] values)
            where T : struct, IEquatable<T>
        {
            for (var i = 0L; i < values.LongLength; i++)
            {
                if (values[i].Equals(value))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to get value from nullable container.
        /// </summary>
        /// <typeparam name="T">The underlying value type of the nullable type.</typeparam>
        /// <param name="nullable">Nullable value.</param>
        /// <param name="value">Underlying value.</param>
        /// <returns><see langword="true"/> if <paramref name="nullable"/> is not <see langword="null"/>; otherwise, <see langword="false"/>.</returns>
        public static bool TryGetValue<T>(this T? nullable, out T value)
            where T : struct
        {
            value = nullable.GetValueOrDefault();
            return nullable.HasValue;
        }

        /// <summary>
        /// Converts <see cref="IntPtr"/> into <see cref="UIntPtr"/>.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted <paramref name="value"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr ToUIntPtr(this IntPtr value)
        {
            Push(value);
            Conv_U();
            return Return<UIntPtr>();
        }

        /// <summary>
        /// Converts <see cref="UIntPtr"/> into <see cref="IntPtr"/>
        /// respecting overflow.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted <paramref name="value"/>.</returns>
        /// <exception cref="OverflowException"><paramref name="value"/> is less than zero.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr ToUIntPtrChecked(this IntPtr value)
        {
            Push(value);
            Conv_Ovf_U();
            return Return<UIntPtr>();
        }

        /// <summary>
        /// Converts <see cref="bool"/> into <see cref="int"/>.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><see cref="int"/> representation of <paramref name="value"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToInt32(this bool value)
        {
            Push(value);
            return Return<int>();
        }

        /// <summary>
        /// Converts <see cref="bool"/> into <see cref="byte"/>.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><see cref="byte"/> representation of <paramref name="value"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ToByte(this bool value)
        {
            Push(value);
            Conv_U1();
            return Return<byte>();
        }

        /// <summary>
        /// Converts <see cref="int"/> into <see cref="bool"/>.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><see langword="true"/> if <c>value != 0</c>; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ToBoolean(this int value) => value != 0;

        /// <summary>
        /// Converts <see cref="UIntPtr"/> into <see cref="IntPtr"/>.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted <paramref name="value"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static IntPtr ToIntPtr(this UIntPtr value)
        {
            Push(value);
            Conv_I();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Converts <see cref="UIntPtr"/> into <see cref="IntPtr"/>
        /// respecting overflow.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted <paramref name="value"/>.</returns>
        /// <exception cref="OverflowException"><paramref name="value"/> is greater than the maximum positive signed native integer.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static IntPtr ToIntPtrChecked(this UIntPtr value)
        {
            Push(value);
            Conv_Ovf_I_Un();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Determines whether the native integer is less than the specified value.
        /// </summary>
        /// <param name="value">The value to compare with other value.</param>
        /// <param name="comparand">he value that is compared by value to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is less than <paramref name="comparand"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool LessThan(this IntPtr value, IntPtr comparand)
        {
            Push(value);
            Push(comparand);
            Clt();
            return Return<bool>();
        }

        /// <summary>
        /// Determines whether the native integer is greater than the specified value.
        /// </summary>
        /// <param name="value">The value to compare with other value.</param>
        /// <param name="comparand">he value that is compared by value to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is greater than <paramref name="comparand"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GreaterThan(this IntPtr value, IntPtr comparand)
        {
            Push(value);
            Push(comparand);
            Cgt();
            return Return<bool>();
        }

        /// <summary>
        /// Determines whether the native integer is greater than or equal to specified value.
        /// </summary>
        /// <param name="value">The value to compare with other value.</param>
        /// <param name="comparand">he value that is compared by value to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is greater than or equal to <paramref name="comparand"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GreaterThanOrEqual(this IntPtr value, IntPtr comparand)
        {
            Push(value);
            Push(comparand);
            Clt();
            Ldc_I4_0();
            Ceq();
            return Return<bool>();
        }

        /// <summary>
        /// Determines whether the native integer is less than or equal to specified value.
        /// </summary>
        /// <param name="value">The value to compare with other value.</param>
        /// <param name="comparand">he value that is compared by value to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is less than or equal to <paramref name="comparand"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool LessThanOrEqual(this IntPtr value, IntPtr comparand)
        {
            Push(value);
            Push(comparand);
            Cgt();
            Ldc_I4_0();
            Ceq();
            return Return<bool>();
        }

        /// <summary>
        /// Determines whether the native integer is less than the specified value.
        /// </summary>
        /// <param name="value">The value to compare with other value.</param>
        /// <param name="comparand">he value that is compared by value to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is less than <paramref name="comparand"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static bool LessThan(this UIntPtr value, UIntPtr comparand)
        {
            Push(value);
            Push(comparand);
            Clt_Un();
            return Return<bool>();
        }

        /// <summary>
        /// Determines whether the native integer is greater than the specified value.
        /// </summary>
        /// <param name="value">The value to compare with other value.</param>
        /// <param name="comparand">he value that is compared by value to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is greater than <paramref name="comparand"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static bool GreaterThan(this UIntPtr value, UIntPtr comparand)
        {
            Push(value);
            Push(comparand);
            Cgt_Un();
            return Return<bool>();
        }

        /// <summary>
        /// Determines whether the native integer is greater than or equal to specified value.
        /// </summary>
        /// <param name="value">The value to compare with other value.</param>
        /// <param name="comparand">he value that is compared by value to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is greater than or equal to <paramref name="comparand"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static bool GreaterThanOrEqual(this UIntPtr value, UIntPtr comparand)
        {
            Push(value);
            Push(comparand);
            Clt_Un();
            Ldc_I4_0();
            Ceq();
            return Return<bool>();
        }

        /// <summary>
        /// Determines whether the native integer is less than or equal to specified value.
        /// </summary>
        /// <param name="value">The value to compare with other value.</param>
        /// <param name="comparand">he value that is compared by value to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is less than or equal to <paramref name="comparand"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static bool LessThanOrEqual(this UIntPtr value, UIntPtr comparand)
        {
            Push(value);
            Push(comparand);
            Cgt_Un();
            Ldc_I4_0();
            Ceq();
            return Return<bool>();
        }

        /// <summary>
        /// Negates native integer value.
        /// </summary>
        /// <param name="value">The value to negate.</param>
        /// <returns>The negated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr Negate(this IntPtr value)
        {
            Push(value);
            Neg();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Adds two specified native integers.
        /// </summary>
        /// <param name="x">The first value to add.</param>
        /// <param name="y">The second value to add.</param>
        /// <returns>The result of adding <paramref name="x"/> and <paramref name="y"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr Add(this IntPtr x, IntPtr y)
        {
            Push(x);
            Push(y);
            Emit.Add();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Adds two specified native integers.
        /// </summary>
        /// <param name="x">The first value to add.</param>
        /// <param name="y">The second value to add.</param>
        /// <returns>The result of adding <paramref name="x"/> and <paramref name="y"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr Add(this UIntPtr x, UIntPtr y)
        {
            Push(x);
            Push(y);
            Emit.Add();
            return Return<UIntPtr>();
        }

        /// <summary>
        /// Adds two specified native integers.
        /// </summary>
        /// <param name="x">The first value to add.</param>
        /// <param name="y">The second value to add.</param>
        /// <returns>The result of adding <paramref name="x"/> and <paramref name="y"/>.</returns>
        /// <exception cref="OverflowException">The result of an operation is outside the bounds of the <see cref="IntPtr"/> data type.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr AddChecked(this IntPtr x, IntPtr y)
        {
            Push(x);
            Push(y);
            Add_Ovf();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Adds two specified native integers.
        /// </summary>
        /// <param name="x">The first value to add.</param>
        /// <param name="y">The second value to add.</param>
        /// <returns>The result of adding <paramref name="x"/> and <paramref name="y"/>.</returns>
        /// <exception cref="OverflowException">The result of an operation is outside the bounds of the <see cref="UIntPtr"/> data type.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr AddChecked(this UIntPtr x, UIntPtr y)
        {
            Push(x);
            Push(y);
            Add_Ovf_Un();
            return Return<UIntPtr>();
        }

        /// <summary>
        /// Subtracts two native integers.
        /// </summary>
        /// <param name="x">The minuend.</param>
        /// <param name="y">The subtrahend.</param>
        /// <returns>The result of subtracting <paramref name="y"/> from <paramref name="x"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr Subtract(this IntPtr x, IntPtr y)
        {
            Push(x);
            Push(y);
            Sub();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Subtracts two native integers.
        /// </summary>
        /// <param name="x">The minuend.</param>
        /// <param name="y">The subtrahend.</param>
        /// <returns>The result of subtracting <paramref name="y"/> from <paramref name="x"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr Subtract(this UIntPtr x, UIntPtr y)
        {
            Push(x);
            Push(y);
            Sub();
            return Return<UIntPtr>();
        }

        /// <summary>
        /// Subtracts two native integers.
        /// </summary>
        /// <param name="x">The minuend.</param>
        /// <param name="y">The subtrahend.</param>
        /// <returns>The result of subtracting <paramref name="y"/> from <paramref name="x"/>.</returns>
        /// <exception cref="OverflowException">The result of an operation is outside the bounds of the <see cref="IntPtr"/> data type.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr SubtractChecked(this IntPtr x, IntPtr y)
        {
            Push(x);
            Push(y);
            Sub_Ovf();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Subtracts two native integers.
        /// </summary>
        /// <param name="x">The minuend.</param>
        /// <param name="y">The subtrahend.</param>
        /// <returns>The result of subtracting <paramref name="y"/> from <paramref name="x"/>.</returns>
        /// <exception cref="OverflowException">The result of an operation is outside the bounds of the <see cref="UIntPtr"/> data type.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr SubtractChecked(this UIntPtr x, UIntPtr y)
        {
            Push(x);
            Push(y);
            Sub_Ovf_Un();
            return Return<UIntPtr>();
        }

        /// <summary>
        /// Multiplies two specified native integers.
        /// </summary>
        /// <param name="x">The first value to multiply.</param>
        /// <param name="y">The second value to mulitply.</param>
        /// <returns>The result of multiplying <paramref name="x"/> by <paramref name="y"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr Multiply(this IntPtr x, IntPtr y)
        {
            Push(x);
            Push(y);
            Mul();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Multiplies two specified native integers.
        /// </summary>
        /// <param name="x">The first value to multiply.</param>
        /// <param name="y">The second value to mulitply.</param>
        /// <returns>The result of multiplying <paramref name="x"/> by <paramref name="y"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr Multiply(this UIntPtr x, UIntPtr y)
        {
            Push(x);
            Push(y);
            Mul();
            return Return<UIntPtr>();
        }

        /// <summary>
        /// Multiplies two specified native integers.
        /// </summary>
        /// <param name="x">The first value to multiply.</param>
        /// <param name="y">The second value to mulitply.</param>
        /// <returns>The result of multiplying <paramref name="x"/> by <paramref name="y"/>.</returns>
        /// <exception cref="OverflowException">The result of an operation is outside the bounds of the <see cref="IntPtr"/> data type.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr MultiplyChecked(this IntPtr x, IntPtr y)
        {
            Push(x);
            Push(y);
            Mul_Ovf();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Multiplies two specified native integers.
        /// </summary>
        /// <param name="x">The first value to multiply.</param>
        /// <param name="y">The second value to mulitply.</param>
        /// <returns>The result of multiplying <paramref name="x"/> by <paramref name="y"/>.</returns>
        /// <exception cref="OverflowException">The result of an operation is outside the bounds of the <see cref="IntPtr"/> data type.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr MultiplyChecked(this UIntPtr x, UIntPtr y)
        {
            Push(x);
            Push(y);
            Mul_Ovf_Un();
            return Return<UIntPtr>();
        }

        /// <summary>
        /// Divides two specified native integers.
        /// </summary>
        /// <param name="x">The dividend.</param>
        /// <param name="y">The divisor.</param>
        /// <returns>The result of dividing <paramref name="x"/> by <paramref name="y"/>.</returns>
        /// <exception cref="DivideByZeroException"><paramref name="y"/> is equal to <see cref="IntPtr.Zero"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr Divide(this IntPtr x, IntPtr y)
        {
            Push(x);
            Push(y);
            Div();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Divides two values and returns the remainder.
        /// </summary>
        /// <param name="x">The dividend.</param>
        /// <param name="y">The divisor.</param>
        /// <returns>The remainder.</returns>
        /// <exception cref="DivideByZeroException"><paramref name="y"/> is equal to <see cref="IntPtr.Zero"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr Remainder(this IntPtr x, IntPtr y)
        {
            Push(x);
            Push(y);
            Rem();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Divides two specified native integers.
        /// </summary>
        /// <param name="x">The dividend.</param>
        /// <param name="y">The divisor.</param>
        /// <returns>The result of dividing <paramref name="x"/> by <paramref name="y"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr Divide(this UIntPtr x, UIntPtr y)
        {
            Push(x);
            Push(y);
            Div_Un();
            return Return<UIntPtr>();
        }

        /// <summary>
        /// Divides two values and returns the remainder.
        /// </summary>
        /// <param name="x">The dividend.</param>
        /// <param name="y">The divisor.</param>
        /// <returns>The remainder.</returns>
        /// <exception cref="DivideByZeroException"><paramref name="y"/> is equal to <see cref="UIntPtr.Zero"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr Remainder(this UIntPtr x, UIntPtr y)
        {
            Push(x);
            Push(y);
            Rem_Un();
            return Return<UIntPtr>();
        }

        /// <summary>
        /// Computes the bitwise XOR of two native integer values.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>The bitwise XOR.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr Xor(this IntPtr x, IntPtr y)
        {
            Push(x);
            Push(y);
            Emit.Xor();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Computes the bitwise XOR of two native integer values.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>The bitwise XOR.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr Xor(this UIntPtr x, UIntPtr y)
        {
            Push(x);
            Push(y);
            Emit.Xor();
            return Return<UIntPtr>();
        }

        /// <summary>
        /// Computes the bitwise AND of two native integer values.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>The bitwise AND.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr And(this UIntPtr x, UIntPtr y)
        {
            Push(x);
            Push(y);
            Emit.And();
            return Return<UIntPtr>();
        }

        /// <summary>
        /// Computes the bitwise AND of two native integer values.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>The bitwise AND.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr And(this IntPtr x, IntPtr y)
        {
            Push(x);
            Push(y);
            Emit.And();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Computes the bitwise OR of two native integer values.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>The bitwise OR.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr Or(this IntPtr x, IntPtr y)
        {
            Push(x);
            Push(y);
            Emit.Or();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Computes the bitwise OR of two native integer values.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>The bitwise OR.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr Or(this UIntPtr x, UIntPtr y)
        {
            Push(x);
            Push(y);
            Emit.Or();
            return Return<UIntPtr>();
        }

        /// <summary>
        /// Computes the bitwise complement of native integer value.
        /// </summary>
        /// <param name="value">The operand.</param>
        /// <returns>The result of bitwise complement.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr OnesComplement(this IntPtr value)
        {
            Push(value);
            Not();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Computes the bitwise complement of native integer value.
        /// </summary>
        /// <param name="value">The operand.</param>
        /// <returns>The result of bitwise complement.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr OnesComplement(this UIntPtr value)
        {
            Push(value);
            Not();
            return Return<UIntPtr>();
        }

        /// <summary>
        /// Shifts native integer value to the left by a specified number of bits.
        /// </summary>
        /// <param name="value">The value to shift.</param>
        /// <param name="bits">The numbers of bits to shift.</param>
        /// <returns>The modified value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr LeftShift(this IntPtr value, IntPtr bits)
        {
            Push(value);
            Push(bits);
            Shl();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Shifts native integer value to the left by a specified number of bits.
        /// </summary>
        /// <param name="value">The value to shift.</param>
        /// <param name="bits">The numbers of bits to shift.</param>
        /// <returns>The modified value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr LeftShift(this IntPtr value, int bits)
            => LeftShift(value, new IntPtr(bits));

        /// <summary>
        /// Shifts native integer value to the right by a specified number of bits.
        /// </summary>
        /// <param name="value">The value to shift.</param>
        /// <param name="bits">The numbers of bits to shift.</param>
        /// <returns>The modified value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr RightShift(this IntPtr value, IntPtr bits)
        {
            Push(value);
            Push(bits);
            Shr();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Shifts native integer value to the right by a specified number of bits.
        /// </summary>
        /// <param name="value">The value to shift.</param>
        /// <param name="bits">The numbers of bits to shift.</param>
        /// <returns>The modified value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr RightShift(this IntPtr value, int bits)
            => RightShift(value, new IntPtr(bits));

        /// <summary>
        /// Shifts native integer value to the left by a specified number of bits.
        /// </summary>
        /// <param name="value">The value to shift.</param>
        /// <param name="bits">The numbers of bits to shift.</param>
        /// <returns>The modified value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr LeftShift(this UIntPtr value, IntPtr bits)
        {
            Push(value);
            Push(bits);
            Shl();
            return Return<UIntPtr>();
        }

        /// <summary>
        /// Shifts native integer value to the left by a specified number of bits.
        /// </summary>
        /// <param name="value">The value to shift.</param>
        /// <param name="bits">The numbers of bits to shift.</param>
        /// <returns>The modified value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr LeftShift(this UIntPtr value, int bits)
            => LeftShift(value, new IntPtr(bits));

        /// <summary>
        /// Shifts native integer value to the right by a specified number of bits.
        /// </summary>
        /// <param name="value">The value to shift.</param>
        /// <param name="bits">The numbers of bits to shift.</param>
        /// <returns>The modified value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr RightShift(this UIntPtr value, IntPtr bits)
        {
            Push(value);
            Push(bits);
            Shr();
            return Return<UIntPtr>();
        }

        /// <summary>
        /// Shifts native integer value to the right by a specified number of bits.
        /// </summary>
        /// <param name="value">The value to shift.</param>
        /// <param name="bits">The numbers of bits to shift.</param>
        /// <returns>The modified value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr RightShift(this UIntPtr value, int bits)
            => RightShift(value, new IntPtr(bits));

        /// <summary>
        /// Increments native integer by 1.
        /// </summary>
        /// <param name="value">The value to increment.</param>
        /// <returns>The incremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr Increment(this IntPtr value) => Add(value, new IntPtr(1));

        /// <summary>
        /// Decrements native integer by 1.
        /// </summary>
        /// <param name="value">The value to increment.</param>
        /// <returns>The decremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr Decrement(this IntPtr value) => Subtract(value, new IntPtr(1));

        /// <summary>
        /// Increments native integer by 1.
        /// </summary>
        /// <param name="value">The value to increment.</param>
        /// <returns>The incremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr Increment(this UIntPtr value) => Add(value, new UIntPtr(1));

        /// <summary>
        /// Decrements native integer by 1.
        /// </summary>
        /// <param name="value">The value to increment.</param>
        /// <returns>The decremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static UIntPtr Decrement(this UIntPtr value) => Subtract(value, new UIntPtr(1));

        /// <summary>
        /// Throws <see cref="ArithmeticException"/> if the value
        /// is "not a number" (NaN), positive or negative infinity.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>The value that is equal to <paramref name="value"/>.</returns>
        /// <exception cref="ArithmeticException"><paramref name="value"/> is not a number.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EnsureFinite(this float value)
        {
            Push(value);
            Ckfinite();
            return Return<float>();
        }

        /// <summary>
        /// Throws <see cref="ArithmeticException"/> if the value
        /// is "not a number" (NaN), positive or negative infinity.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>The value that is equal to <paramref name="value"/>.</returns>
        /// <exception cref="ArithmeticException"><paramref name="value"/> is not a number.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double EnsureFinite(this double value)
        {
            Push(value);
            Ckfinite();
            return Return<double>();
        }

        /// <summary>
        /// Truncates 64-bit signed integer.
        /// </summary>
        /// <param name="value">The value to truncate.</param>
        /// <returns><see cref="int.MaxValue"/> if <paramref name="value"/> is greater than <see cref="int.MaxValue"/>; otherwise, cast <paramref name="value"/> to <see cref="int"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Truncate(this long value) => value > int.MaxValue ? int.MaxValue : (int)value;
    }
}