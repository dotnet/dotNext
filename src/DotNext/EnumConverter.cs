using System;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;

namespace DotNext
{
    /// <summary>
    /// Provides conversion between enum value and primitive types.
    /// </summary>
    public static class EnumConverter
    {
        private static O ToEnum<I, O>(I value)
            where I : struct, IConvertible, IComparable<I>, IEquatable<I>, IFormattable
            where O : struct, Enum
        {
            switch(Type.GetTypeCode(typeof(O)))
            {
                case TypeCode.Byte:
                    Ldarg(nameof(value));
                    Conv_U1();
                    break;
                case TypeCode.Int16:
                    Ldarg(nameof(value));
                    Conv_I2();
                    break;
                case TypeCode.Int32:
                    Ldarg(nameof(value));
                    Conv_I4();
                    break;
                case TypeCode.Int64:
                    Ldarg(nameof(value));
                    Conv_I8();
                    break;
                case TypeCode.Single:
                    Ldarg(nameof(value));
                    Conv_R4();
                    break;
                case TypeCode.Double:
                    Ldarg(nameof(value));
                    Conv_R8();
                    break;
                case TypeCode.SByte:
                    Ldarg(nameof(value));
                    Conv_U1();
                    break;
                case TypeCode.UInt16:
                    Ldarg(nameof(value));
                    Conv_U2();
                    break;
                case TypeCode.UInt32:
                    Ldarg(nameof(value));
                    Conv_U4();
                    break;
                case TypeCode.UInt64:
                    Ldarg(nameof(value));
                    Conv_U8();
                    break;
                default:
                    throw new GenericArgumentException(typeof(O), ExceptionMessages.UnsupportedEnumType);
            }
            return Return<O>();
        }

        /// <summary>
        /// Converts <see cref="long"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="long"/> value.</returns>
        public static T ToEnum<T>(this long value) where T : struct, Enum => ToEnum<long, T>(value);

        /// <summary>
        /// Converts <see cref="int"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="int"/> value.</returns>
        public static T ToEnum<T>(this int value) where T : struct, Enum => ToEnum<int, T>(value);

        /// <summary>
        /// Converts <see cref="short"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="short"/> value.</returns>
        public static T ToEnum<T>(this short value) where T : struct, Enum => ToEnum<short, T>(value);

        /// <summary>
        /// Converts <see cref="byte"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="byte"/> value.</returns>
        public static T ToEnum<T>(this byte value) where T : struct, Enum => ToEnum<byte, T>(value);

        /// <summary>
        /// Converts <see cref="byte"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="byte"/> value.</returns>
        [CLSCompliant(false)]
        public static T ToEnum<T>(this sbyte value) where T : struct, Enum => ToEnum<sbyte, T>(value);

        /// <summary>
        /// Converts <see cref="ushort"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="ushort"/> value.</returns>
        [CLSCompliant(false)]
        public static T ToEnum<T>(this ushort value) where T : struct, Enum => ToEnum<ushort, T>(value);

        /// <summary>
        /// Converts <see cref="uint"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="uint"/> value.</returns>
        [CLSCompliant(false)]
        public static T ToEnum<T>(this uint value) where T : struct, Enum => ToEnum<uint, T>(value);

        /// <summary>
        /// Converts <see cref="ulong"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="ulong"/> value.</returns>
        [CLSCompliant(false)]
        public static T ToEnum<T>(this ulong value) where T : struct, Enum => ToEnum<ulong, T>(value);

        /// <summary>
        /// Converts enum value into primitive type <see cref="long"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="long"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ToInt64<T>(this T value) 
            where T : struct, Enum
        {
            Ldarg(nameof(value));
            Conv_I8();
            return Return<long>();
        }

        /// <summary>
        /// Converts enum value into primitive type <see cref="int"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="int"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToInt32<T>(this T value) 
            where T : struct, Enum
        {
            Ldarg(nameof(value));
            Conv_I4();
            return Return<int>();
        }

        /// <summary>
        /// Converts enum value into primitive type <see cref="short"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="short"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ToInt16<T>(this T value) 
            where T : struct, Enum
        {
            Ldarg(nameof(value));
            Conv_I2();
            return Return<short>();
        }

        /// <summary>
        /// Converts enum value into primitive type <see cref="byte"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="byte"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ToByte<T>(this T value) 
            where T : struct, Enum
        {
            Ldarg(nameof(value));
            Conv_U1();
            return Return<byte>();
        }

        /// <summary>
        /// Converts enum value into primitive type <see cref="ulong"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="ulong"/>.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ToUInt64<T>(this T value) 
            where T : struct, Enum
        {
            Ldarg(nameof(value));
            Conv_U8();
            return Return<ulong>();
        }

        /// <summary>
        /// Converts enum value into primitive type <see cref="uint"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="uint"/>.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ToUInt32<T>(this T value) 
            where T : struct, Enum
        {
            Ldarg(nameof(value));
            Conv_U4();
            return Return<uint>();
        }

        /// <summary>
        /// Converts enum value into primitive type <see cref="ushort"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="ushort"/>.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ToUInt16<T>(this T value) 
            where T : struct, Enum
        {
            Ldarg(nameof(value));
            Conv_U2();
            return Return<ushort>();
        }

        /// <summary>
        /// Converts enum value into primitive type <see cref="sbyte"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="sbyte"/>.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte ToSByte<T>(this T value) 
            where T : struct, Enum
        {
            Ldarg(nameof(value));
            Conv_I1();
            return Return<sbyte>();
        }

        /// <summary>
        /// Determines whether two enum values are equal without boxing.
        /// </summary>
        /// <param name="first">The first value to compare.</param>
        /// <param name="other">The second value to compare.</param>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <returns><see langword="true"/> if both values are equal; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals<T>(this T first, T other)
            where T : struct, Enum
        {
            Ldarg(nameof(first));
            Ldarg(nameof(other));
            Ceq();
            return Return<bool>();
        }
    }
}
