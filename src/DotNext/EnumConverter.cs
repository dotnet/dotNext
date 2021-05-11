using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using static System.Globalization.CultureInfo;

namespace DotNext
{
    using Intrinsics = Runtime.Intrinsics;

    internal static unsafe class EnumConverter<TInput, TOutput>
            where TInput : struct, IConvertible, IComparable, IFormattable
            where TOutput : struct, IConvertible, IComparable, IFormattable
    {
        private static readonly delegate*<TInput, TOutput> Converter;

#if !NETSTANDARD2_1
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(Convert))]
#endif
        static EnumConverter()
        {
            var conversionMethod = System.Type.GetTypeCode(typeof(TOutput)) switch
            {
                TypeCode.Byte => nameof(System.Convert.ToByte),
                TypeCode.SByte => nameof(System.Convert.ToSByte),
                TypeCode.Int16 => nameof(System.Convert.ToInt16),
                TypeCode.UInt16 => nameof(System.Convert.ToUInt16),
                TypeCode.Int32 => nameof(System.Convert.ToInt32),
                TypeCode.UInt32 => nameof(System.Convert.ToUInt32),
                TypeCode.Int64 => nameof(System.Convert.ToInt64),
                TypeCode.UInt64 => nameof(System.Convert.ToUInt64),
                TypeCode.Boolean => nameof(System.Convert.ToBoolean),
                TypeCode.Single => nameof(System.Convert.ToSingle),
                TypeCode.Double => nameof(System.Convert.ToDouble),
                TypeCode.Char => nameof(System.Convert.ToChar),
                TypeCode.Decimal => nameof(System.Convert.ToDecimal),
                TypeCode.DateTime => nameof(System.Convert.ToDateTime),
                _ => "<unknown>",
            };
            var type = typeof(TInput);
            if (type.IsEnum)
                type = type.GetEnumUnderlyingType();

            // find conversion method using Reflection
            MethodInfo? method = typeof(Convert).GetMethod(conversionMethod, new[] { type });
            if (method is null)
            {
                Converter = &ConvertSlow;
            }
            else
            {
                Debug.Assert(method.IsStatic && method.IsPublic);
                Converter = (delegate*<TInput, TOutput>)method.MethodHandle.GetFunctionPointer();
            }

            static TOutput ConvertSlow(TInput value) => (TOutput)value.ToType(typeof(TOutput), CurrentCulture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TOutput Convert(TInput value)
            => Unsafe.SizeOf<TInput>() == Unsafe.SizeOf<TOutput>() ? Unsafe.As<TInput, TOutput>(ref value) : Converter(value);
    }

    /// <summary>
    /// Provides conversion between enum value and primitive types.
    /// </summary>
    public static class EnumConverter
    {
        /// <summary>
        /// Converts <see cref="long"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="long"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ToEnum<T>(this long value)
            where T : struct, Enum
            => EnumConverter<long, T>.Convert(value);

        /// <summary>
        /// Converts <see cref="int"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="int"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ToEnum<T>(this int value)
            where T : struct, Enum
            => EnumConverter<int, T>.Convert(value);

        /// <summary>
        /// Converts <see cref="short"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="short"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ToEnum<T>(this short value)
            where T : struct, Enum
            => EnumConverter<short, T>.Convert(value);

        /// <summary>
        /// Converts <see cref="byte"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="byte"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ToEnum<T>(this byte value)
            where T : struct, Enum
            => EnumConverter<byte, T>.Convert(value);

        /// <summary>
        /// Converts <see cref="byte"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="byte"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static T ToEnum<T>(this sbyte value)
            where T : struct, Enum
            => EnumConverter<sbyte, T>.Convert(value);

        /// <summary>
        /// Converts <see cref="ushort"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="ushort"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static T ToEnum<T>(this ushort value)
            where T : struct, Enum
            => EnumConverter<ushort, T>.Convert(value);

        /// <summary>
        /// Converts <see cref="uint"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="uint"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static T ToEnum<T>(this uint value)
            where T : struct, Enum
            => EnumConverter<uint, T>.Convert(value);

        /// <summary>
        /// Converts <see cref="ulong"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="ulong"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static T ToEnum<T>(this ulong value)
            where T : struct, Enum
            => EnumConverter<ulong, T>.Convert(value);

        /// <summary>
        /// Converts enum value into primitive type <see cref="long"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="long"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ToInt64<T>(this T value)
            where T : struct, Enum
            => EnumConverter<T, long>.Convert(value);

        /// <summary>
        /// Converts enum value into primitive type <see cref="int"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="int"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToInt32<T>(this T value)
            where T : struct, Enum
            => EnumConverter<T, int>.Convert(value);

        /// <summary>
        /// Converts enum value into primitive type <see cref="short"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="short"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ToInt16<T>(this T value)
            where T : struct, Enum
            => EnumConverter<T, short>.Convert(value);

        /// <summary>
        /// Converts enum value into primitive type <see cref="byte"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="byte"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ToByte<T>(this T value)
            where T : struct, Enum
            => EnumConverter<T, byte>.Convert(value);

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
            => EnumConverter<T, ulong>.Convert(value);

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
            => EnumConverter<T, uint>.Convert(value);

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
            => EnumConverter<T, ushort>.Convert(value);

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
            => EnumConverter<T, sbyte>.Convert(value);

        /// <summary>
        /// Checks whether the specified value is equal to one
        /// of the specified values.
        /// </summary>
        /// <typeparam name="T">The type of object to compare.</typeparam>
        /// <param name="value">The value to compare with other.</param>
        /// <param name="values">Candidate objects.</param>
        /// <returns><see langword="true"/>, if <paramref name="value"/> is equal to one of <paramref name="values"/>.</returns>
        [Obsolete("Use pattern-matching expression in C#")]
        public static bool IsOneOf<T>(this T value, params T[] values)
            where T : struct, Enum
        {
            for (nint i = 0; i < Intrinsics.GetLength(values); i++)
            {
                if (EqualityComparer<T>.Default.Equals(value, values[i]))
                    return true;
            }

            return false;
        }
    }
}
