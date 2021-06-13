using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static System.Globalization.CultureInfo;
using static InlineIL.IL;
using static InlineIL.IL.Emit;

namespace DotNext
{
    using Intrinsics = Runtime.Intrinsics;

    /// <summary>
    /// Various extensions for value types.
    /// </summary>
    public static class ValueTypeExtensions
    {
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
            for (nint i = 0; i < Intrinsics.GetLength(values); i++)
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
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr ToUIntPtr(this IntPtr value)
            => unchecked((nuint)(nint)value);

        /// <summary>
        /// Converts <see cref="UIntPtr"/> into <see cref="IntPtr"/>
        /// respecting overflow.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted <paramref name="value"/>.</returns>
        /// <exception cref="OverflowException"><paramref name="value"/> is less than zero.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr ToUIntPtrChecked(this IntPtr value)
            => checked((nuint)(nint)value);

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
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr ToIntPtr(this UIntPtr value)
            => unchecked((nint)(nuint)value);

        /// <summary>
        /// Converts <see cref="UIntPtr"/> into <see cref="IntPtr"/>
        /// respecting overflow.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted <paramref name="value"/>.</returns>
        /// <exception cref="OverflowException"><paramref name="value"/> is greater than the maximum positive signed native integer.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr ToIntPtrChecked(this UIntPtr value)
            => checked((nint)(nuint)value);

        /// <summary>
        /// Determines whether the native integer is less than the specified value.
        /// </summary>
        /// <param name="value">The value to compare with other value.</param>
        /// <param name="comparand">he value that is compared by value to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is less than <paramref name="comparand"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static bool LessThan(this IntPtr value, IntPtr comparand)
            => (nint)value < comparand;

        /// <summary>
        /// Determines whether the native integer is greater than the specified value.
        /// </summary>
        /// <param name="value">The value to compare with other value.</param>
        /// <param name="comparand">he value that is compared by value to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is greater than <paramref name="comparand"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static bool GreaterThan(this IntPtr value, IntPtr comparand)
            => (nint)value > comparand;

        /// <summary>
        /// Determines whether the native integer is greater than or equal to specified value.
        /// </summary>
        /// <param name="value">The value to compare with other value.</param>
        /// <param name="comparand">he value that is compared by value to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is greater than or equal to <paramref name="comparand"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static bool GreaterThanOrEqual(this IntPtr value, IntPtr comparand)
            => (nint)value >= comparand;

        /// <summary>
        /// Determines whether the native integer is less than or equal to specified value.
        /// </summary>
        /// <param name="value">The value to compare with other value.</param>
        /// <param name="comparand">he value that is compared by value to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is less than or equal to <paramref name="comparand"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static bool LessThanOrEqual(this IntPtr value, IntPtr comparand)
            => (nint)value <= comparand;

        /// <summary>
        /// Determines whether the native integer is less than the specified value.
        /// </summary>
        /// <param name="value">The value to compare with other value.</param>
        /// <param name="comparand">he value that is compared by value to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is less than <paramref name="comparand"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static bool LessThan(this UIntPtr value, UIntPtr comparand)
            => (nuint)value < comparand;

        /// <summary>
        /// Determines whether the native integer is greater than the specified value.
        /// </summary>
        /// <param name="value">The value to compare with other value.</param>
        /// <param name="comparand">he value that is compared by value to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is greater than <paramref name="comparand"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static bool GreaterThan(this UIntPtr value, UIntPtr comparand)
            => (nuint)value > comparand;

        /// <summary>
        /// Determines whether the native integer is greater than or equal to specified value.
        /// </summary>
        /// <param name="value">The value to compare with other value.</param>
        /// <param name="comparand">he value that is compared by value to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is greater than or equal to <paramref name="comparand"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static bool GreaterThanOrEqual(this UIntPtr value, UIntPtr comparand)
            => (nuint)value >= comparand;

        /// <summary>
        /// Determines whether the native integer is less than or equal to specified value.
        /// </summary>
        /// <param name="value">The value to compare with other value.</param>
        /// <param name="comparand">he value that is compared by value to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is less than or equal to <paramref name="comparand"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static bool LessThanOrEqual(this UIntPtr value, UIntPtr comparand)
            => (nuint)value <= comparand;

        /// <summary>
        /// Negates native integer value.
        /// </summary>
        /// <param name="value">The value to negate.</param>
        /// <returns>The negated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr Negate(this IntPtr value)
            => -(nint)value;

        /// <summary>
        /// Adds two specified native integers.
        /// </summary>
        /// <param name="x">The first value to add.</param>
        /// <param name="y">The second value to add.</param>
        /// <returns>The result of adding <paramref name="x"/> and <paramref name="y"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr Add(this IntPtr x, IntPtr y)
            => unchecked((nint)x + y);

        /// <summary>
        /// Adds two specified native integers.
        /// </summary>
        /// <param name="x">The first value to add.</param>
        /// <param name="y">The second value to add.</param>
        /// <returns>The result of adding <paramref name="x"/> and <paramref name="y"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr Add(this UIntPtr x, UIntPtr y)
            => unchecked((nuint)x + y);

        /// <summary>
        /// Adds two specified native integers.
        /// </summary>
        /// <param name="x">The first value to add.</param>
        /// <param name="y">The second value to add.</param>
        /// <returns>The result of adding <paramref name="x"/> and <paramref name="y"/>.</returns>
        /// <exception cref="OverflowException">The result of an operation is outside the bounds of the <see cref="IntPtr"/> data type.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr AddChecked(this IntPtr x, IntPtr y)
            => checked((nint)x + y);

        /// <summary>
        /// Adds two specified native integers.
        /// </summary>
        /// <param name="x">The first value to add.</param>
        /// <param name="y">The second value to add.</param>
        /// <returns>The result of adding <paramref name="x"/> and <paramref name="y"/>.</returns>
        /// <exception cref="OverflowException">The result of an operation is outside the bounds of the <see cref="UIntPtr"/> data type.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr AddChecked(this UIntPtr x, UIntPtr y)
            => checked((nuint)x + y);

        /// <summary>
        /// Subtracts two native integers.
        /// </summary>
        /// <param name="x">The minuend.</param>
        /// <param name="y">The subtrahend.</param>
        /// <returns>The result of subtracting <paramref name="y"/> from <paramref name="x"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr Subtract(this IntPtr x, IntPtr y)
            => unchecked((nint)x - y);

        /// <summary>
        /// Subtracts two native integers.
        /// </summary>
        /// <param name="x">The minuend.</param>
        /// <param name="y">The subtrahend.</param>
        /// <returns>The result of subtracting <paramref name="y"/> from <paramref name="x"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr Subtract(this UIntPtr x, UIntPtr y)
            => unchecked((nuint)x - y);

        /// <summary>
        /// Subtracts two native integers.
        /// </summary>
        /// <param name="x">The minuend.</param>
        /// <param name="y">The subtrahend.</param>
        /// <returns>The result of subtracting <paramref name="y"/> from <paramref name="x"/>.</returns>
        /// <exception cref="OverflowException">The result of an operation is outside the bounds of the <see cref="IntPtr"/> data type.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr SubtractChecked(this IntPtr x, IntPtr y)
            => checked((nint)x - y);

        /// <summary>
        /// Subtracts two native integers.
        /// </summary>
        /// <param name="x">The minuend.</param>
        /// <param name="y">The subtrahend.</param>
        /// <returns>The result of subtracting <paramref name="y"/> from <paramref name="x"/>.</returns>
        /// <exception cref="OverflowException">The result of an operation is outside the bounds of the <see cref="UIntPtr"/> data type.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr SubtractChecked(this UIntPtr x, UIntPtr y)
            => checked((nuint)x - y);

        /// <summary>
        /// Multiplies two specified native integers.
        /// </summary>
        /// <param name="x">The first value to multiply.</param>
        /// <param name="y">The second value to mulitply.</param>
        /// <returns>The result of multiplying <paramref name="x"/> by <paramref name="y"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr Multiply(this IntPtr x, IntPtr y)
            => unchecked((nint)x * y);

        /// <summary>
        /// Multiplies two specified native integers.
        /// </summary>
        /// <param name="x">The first value to multiply.</param>
        /// <param name="y">The second value to mulitply.</param>
        /// <returns>The result of multiplying <paramref name="x"/> by <paramref name="y"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr Multiply(this UIntPtr x, UIntPtr y)
            => unchecked((nuint)x * y);

        /// <summary>
        /// Multiplies two specified native integers.
        /// </summary>
        /// <param name="x">The first value to multiply.</param>
        /// <param name="y">The second value to mulitply.</param>
        /// <returns>The result of multiplying <paramref name="x"/> by <paramref name="y"/>.</returns>
        /// <exception cref="OverflowException">The result of an operation is outside the bounds of the <see cref="IntPtr"/> data type.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr MultiplyChecked(this IntPtr x, IntPtr y)
            => checked((nint)x * y);

        /// <summary>
        /// Multiplies two specified native integers.
        /// </summary>
        /// <param name="x">The first value to multiply.</param>
        /// <param name="y">The second value to mulitply.</param>
        /// <returns>The result of multiplying <paramref name="x"/> by <paramref name="y"/>.</returns>
        /// <exception cref="OverflowException">The result of an operation is outside the bounds of the <see cref="IntPtr"/> data type.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr MultiplyChecked(this UIntPtr x, UIntPtr y)
            => checked((nuint)x * y);

        /// <summary>
        /// Divides two specified native integers.
        /// </summary>
        /// <param name="x">The dividend.</param>
        /// <param name="y">The divisor.</param>
        /// <returns>The result of dividing <paramref name="x"/> by <paramref name="y"/>.</returns>
        /// <exception cref="DivideByZeroException"><paramref name="y"/> is equal to <see cref="IntPtr.Zero"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr Divide(this IntPtr x, IntPtr y)
            => (nint)x / y;

        /// <summary>
        /// Divides two values and returns the remainder.
        /// </summary>
        /// <param name="x">The dividend.</param>
        /// <param name="y">The divisor.</param>
        /// <returns>The remainder.</returns>
        /// <exception cref="DivideByZeroException"><paramref name="y"/> is equal to <see cref="IntPtr.Zero"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr Remainder(this IntPtr x, IntPtr y)
            => (nint)x % y;

        /// <summary>
        /// Divides two specified native integers.
        /// </summary>
        /// <param name="x">The dividend.</param>
        /// <param name="y">The divisor.</param>
        /// <returns>The result of dividing <paramref name="x"/> by <paramref name="y"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr Divide(this UIntPtr x, UIntPtr y)
            => (nuint)x / y;

        /// <summary>
        /// Divides two values and returns the remainder.
        /// </summary>
        /// <param name="x">The dividend.</param>
        /// <param name="y">The divisor.</param>
        /// <returns>The remainder.</returns>
        /// <exception cref="DivideByZeroException"><paramref name="y"/> is equal to <see cref="UIntPtr.Zero"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr Remainder(this UIntPtr x, UIntPtr y)
            => (nuint)x % y;

        /// <summary>
        /// Computes the bitwise XOR of two native integer values.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>The bitwise XOR.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr Xor(this IntPtr x, IntPtr y)
            => (nint)x ^ y;

        /// <summary>
        /// Computes the bitwise XOR of two native integer values.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>The bitwise XOR.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr Xor(this UIntPtr x, UIntPtr y)
            => (nuint)x ^ y;

        /// <summary>
        /// Computes the bitwise AND of two native integer values.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>The bitwise AND.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr And(this UIntPtr x, UIntPtr y)
            => (nuint)x & y;

        /// <summary>
        /// Computes the bitwise AND of two native integer values.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>The bitwise AND.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr And(this IntPtr x, IntPtr y)
            => (nint)x & y;

        /// <summary>
        /// Computes the bitwise OR of two native integer values.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>The bitwise OR.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr Or(this IntPtr x, IntPtr y)
            => (nint)x | y;

        /// <summary>
        /// Computes the bitwise OR of two native integer values.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>The bitwise OR.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr Or(this UIntPtr x, UIntPtr y)
            => (nuint)x | y;

        /// <summary>
        /// Computes the bitwise complement of native integer value.
        /// </summary>
        /// <param name="value">The operand.</param>
        /// <returns>The result of bitwise complement.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr OnesComplement(this IntPtr value)
            => ~(nint)value;

        /// <summary>
        /// Computes the bitwise complement of native integer value.
        /// </summary>
        /// <param name="value">The operand.</param>
        /// <returns>The result of bitwise complement.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr OnesComplement(this UIntPtr value)
            => ~(nuint)value;

        /// <summary>
        /// Shifts native integer value to the left by a specified number of bits.
        /// </summary>
        /// <param name="value">The value to shift.</param>
        /// <param name="bits">The numbers of bits to shift.</param>
        /// <returns>The modified value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr LeftShift(this IntPtr value, int bits)
            => (nint)value << bits;

        /// <summary>
        /// Shifts native integer value to the right by a specified number of bits.
        /// </summary>
        /// <param name="value">The value to shift.</param>
        /// <param name="bits">The numbers of bits to shift.</param>
        /// <returns>The modified value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr RightShift(this IntPtr value, int bits)
            => (nint)value >> bits;

        /// <summary>
        /// Shifts native integer value to the left by a specified number of bits.
        /// </summary>
        /// <param name="value">The value to shift.</param>
        /// <param name="bits">The numbers of bits to shift.</param>
        /// <returns>The modified value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr LeftShift(this UIntPtr value, int bits)
            => (nuint)value << bits;

        /// <summary>
        /// Shifts native integer value to the right by a specified number of bits.
        /// </summary>
        /// <param name="value">The value to shift.</param>
        /// <param name="bits">The numbers of bits to shift.</param>
        /// <returns>The modified value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr RightShift(this UIntPtr value, int bits)
            => (nuint)value >> bits;

        /// <summary>
        /// Increments native integer by 1.
        /// </summary>
        /// <param name="value">The value to increment.</param>
        /// <returns>The incremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr Increment(this IntPtr value) => unchecked((nint)value + 1);

        /// <summary>
        /// Decrements native integer by 1.
        /// </summary>
        /// <param name="value">The value to increment.</param>
        /// <returns>The decremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static IntPtr Decrement(this IntPtr value) => unchecked((nint)value - 1);

        /// <summary>
        /// Increments native integer by 1.
        /// </summary>
        /// <param name="value">The value to increment.</param>
        /// <returns>The incremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr Increment(this UIntPtr value) => unchecked((nuint)value + 1);

        /// <summary>
        /// Decrements native integer by 1.
        /// </summary>
        /// <param name="value">The value to increment.</param>
        /// <returns>The decremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
#if !NETSTANDARD2_1
        [Obsolete("Use nint and nuint data types in C#")]
#endif
        public static UIntPtr Decrement(this UIntPtr value) => unchecked((nuint)value - 1);

        /// <summary>
        /// Truncates 64-bit signed integer.
        /// </summary>
        /// <param name="value">The value to truncate.</param>
        /// <returns><see cref="int.MaxValue"/> if <paramref name="value"/> is greater than <see cref="int.MaxValue"/>; otherwise, cast <paramref name="value"/> to <see cref="int"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Truncate(this long value) => value > int.MaxValue ? int.MaxValue : (int)value;

        /// <summary>
        /// Normalizes value in the specified range.
        /// </summary>
        /// <typeparam name="T">The type of the value to be normalized.</typeparam>
        /// <param name="value">The value to be normalized. Must be in range [min..max].</param>
        /// <param name="min">The lower bound of the value.</param>
        /// <param name="max">The upper bound of the value.</param>
        /// <returns>The normalized value in range [-1..1] for signed value and [0..1] for unsigned value.</returns>
        [CLSCompliant(false)]
        public static float NormalizeToSingle<T>(this T value, T min, T max)
            where T : struct, IConvertible, IComparable<T>
        {
            var v = value.ToSingle(InvariantCulture);
            return value.CompareTo(default) > 0 ?
                v / max.ToSingle(InvariantCulture) :
                -v / min.ToSingle(InvariantCulture);
        }

        /// <summary>
        /// Normalizes value in the specified range.
        /// </summary>
        /// <typeparam name="T">The type of the value to be normalized.</typeparam>
        /// <param name="value">The value to be normalized. Must be in range [min..max].</param>
        /// <param name="min">The lower bound of the value.</param>
        /// <param name="max">The upper bound of the value.</param>
        /// <returns>The normalized value in range [-1..1] for signed value and [0..1] for unsigned value.</returns>
        [CLSCompliant(false)]
        public static double NormalizeToDouble<T>(this T value, T min, T max)
            where T : struct, IConvertible, IComparable<T>
        {
            var v = value.ToDouble(InvariantCulture);
            return value.CompareTo(default) > 0 ?
                v / max.ToDouble(InvariantCulture) :
                -v / min.ToDouble(InvariantCulture);
        }

        /// <summary>
        /// Normalizes 64-bit unsigned integer to interval [0..1).
        /// </summary>
        /// <param name="value">The value to be normalized.</param>
        /// <returns>The normalized value in range [0..1).</returns>
        [CLSCompliant(false)]
        public static double Normalize(this ulong value)
        {
            const ulong fraction = ulong.MaxValue >> (64 - 53);
            const double exponent = (double)(1UL << 53);
            return (fraction & value) / exponent;
        }

        /// <summary>
        /// Normalizes 64-bit signed integer to interval [0..1).
        /// </summary>
        /// <param name="value">The value to be normalized.</param>
        /// <returns>The normalized value in range [0..1).</returns>
        public static double Normalize(this long value)
            => Normalize(unchecked((ulong)value));

        /// <summary>
        /// Normalizes 32-bit unsigned integer to interval [0..1).
        /// </summary>
        /// <param name="value">The value to be normalized.</param>
        /// <returns>The normalized value in range [0..1).</returns>
        [CLSCompliant(false)]
        public static float Normalize(this uint value)
        {
            const uint fraction = uint.MaxValue >> (32 - 24);
            const float exponent = (float)(1U << 24);
            return (fraction & value) / exponent;
        }

        /// <summary>
        /// Normalizes 32-bit signed integer to interval [0..1).
        /// </summary>
        /// <param name="value">The value to be normalized.</param>
        /// <returns>The normalized value in range [0..1).</returns>
        public static float Normalize(this int value)
            => Normalize(unchecked((uint)value));
    }
}