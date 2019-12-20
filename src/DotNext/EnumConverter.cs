using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static System.Globalization.CultureInfo;
using CallSiteDescr = InlineIL.StandAloneMethodSig;

namespace DotNext
{
    internal static class EnumConverter<I, O>
            where I : struct, IConvertible, IComparable, IFormattable
            where O : struct, IConvertible, IComparable, IFormattable
    {
        private static readonly IntPtr converter;

        static EnumConverter()
        {
            string conversionMethod;
            switch (Type.GetTypeCode(typeof(O)))
            {
                default:
                    conversionMethod = "<unknown>";
                    break;
                case TypeCode.Byte:
                    conversionMethod = nameof(System.Convert.ToByte);
                    break;
                case TypeCode.SByte:
                    conversionMethod = nameof(System.Convert.ToSByte);
                    break;
                case TypeCode.Int16:
                    conversionMethod = nameof(System.Convert.ToInt16);
                    break;
                case TypeCode.UInt16:
                    conversionMethod = nameof(System.Convert.ToUInt16);
                    break;
                case TypeCode.Int32:
                    conversionMethod = nameof(System.Convert.ToInt32);
                    break;
                case TypeCode.UInt32:
                    conversionMethod = nameof(System.Convert.ToUInt32);
                    break;
                case TypeCode.Int64:
                    conversionMethod = nameof(System.Convert.ToInt64);
                    break;
                case TypeCode.UInt64:
                    conversionMethod = nameof(System.Convert.ToUInt64);
                    break;
                case TypeCode.Boolean:
                    conversionMethod = nameof(System.Convert.ToBoolean);
                    break;
                case TypeCode.Single:
                    conversionMethod = nameof(System.Convert.ToSingle);
                    break;
                case TypeCode.Double:
                    conversionMethod = nameof(System.Convert.ToDouble);
                    break;
                case TypeCode.Char:
                    conversionMethod = nameof(System.Convert.ToChar);
                    break;
                case TypeCode.Decimal:
                    conversionMethod = nameof(System.Convert.ToDecimal);
                    break;
                case TypeCode.DateTime:
                    conversionMethod = nameof(System.Convert.ToDateTime);
                    break;
            }
            var type = typeof(I);
            if (type.IsEnum)
                type = type.GetEnumUnderlyingType();
            //find conversion method using Reflection
            var method = typeof(Convert).GetMethod(conversionMethod, new[] { type }) ?? new Func<I, O>(ConvertSlow).Method;
            Debug.Assert(method.IsStatic & method.IsPublic);
            converter = method.MethodHandle.GetFunctionPointer();
        }

        private static O ConvertSlow(I value) => (O)value.ToType(typeof(O), CurrentCulture);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static O Convert(I value)
        {
            const string slowPath = "slow";
            //if sizeof(I)==sizeof(O) then do fast path
            Sizeof(typeof(I));
            Sizeof(typeof(O));
            Bne_Un(slowPath);
            Push(ref value);
            Ldobj(typeof(O));
            Ret();

            MarkLabel(slowPath);
            Push(value);
            Push(converter);
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(O), typeof(I)));
            return Return<O>();
        }
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
        public static T ToEnum<T>(this long value) where T : struct, Enum => EnumConverter<long, T>.Convert(value);

        /// <summary>
        /// Converts <see cref="int"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="int"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ToEnum<T>(this int value) where T : struct, Enum => EnumConverter<int, T>.Convert(value);

        /// <summary>
        /// Converts <see cref="short"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="short"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ToEnum<T>(this short value) where T : struct, Enum => EnumConverter<short, T>.Convert(value);

        /// <summary>
        /// Converts <see cref="byte"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="byte"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ToEnum<T>(this byte value) where T : struct, Enum => EnumConverter<byte, T>.Convert(value);

        /// <summary>
        /// Converts <see cref="byte"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="byte"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static T ToEnum<T>(this sbyte value) where T : struct, Enum => EnumConverter<sbyte, T>.Convert(value);

        /// <summary>
        /// Converts <see cref="ushort"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="ushort"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static T ToEnum<T>(this ushort value) where T : struct, Enum => EnumConverter<ushort, T>.Convert(value);

        /// <summary>
        /// Converts <see cref="uint"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="uint"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static T ToEnum<T>(this uint value) where T : struct, Enum => EnumConverter<uint, T>.Convert(value);

        /// <summary>
        /// Converts <see cref="ulong"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equals to the given <see cref="ulong"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static T ToEnum<T>(this ulong value) where T : struct, Enum => EnumConverter<ulong, T>.Convert(value);

        /// <summary>
        /// Converts enum value into primitive type <see cref="long"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="long"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ToInt64<T>(this T value) where T : struct, Enum => EnumConverter<T, long>.Convert(value);

        /// <summary>
        /// Converts enum value into primitive type <see cref="int"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="int"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToInt32<T>(this T value) where T : struct, Enum => EnumConverter<T, int>.Convert(value);

        /// <summary>
        /// Converts enum value into primitive type <see cref="short"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="short"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ToInt16<T>(this T value) where T : struct, Enum => EnumConverter<T, short>.Convert(value);

        /// <summary>
        /// Converts enum value into primitive type <see cref="byte"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="byte"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ToByte<T>(this T value) where T : struct, Enum => EnumConverter<T, byte>.Convert(value);

        /// <summary>
        /// Converts enum value into primitive type <see cref="ulong"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="ulong"/>.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ToUInt64<T>(this T value) where T : struct, Enum => EnumConverter<T, ulong>.Convert(value);

        /// <summary>
        /// Converts enum value into primitive type <see cref="uint"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="uint"/>.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ToUInt32<T>(this T value) where T : struct, Enum => EnumConverter<T, uint>.Convert(value);

        /// <summary>
        /// Converts enum value into primitive type <see cref="ushort"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="ushort"/>.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ToUInt16<T>(this T value) where T : struct, Enum => EnumConverter<T, ushort>.Convert(value);

        /// <summary>
        /// Converts enum value into primitive type <see cref="sbyte"/>.
        /// </summary>
        /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
        /// <param name="value">Enum value to be converted.</param>
        /// <returns>Enum value represented as <see cref="sbyte"/>.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte ToSByte<T>(this T value) where T : struct, Enum => EnumConverter<T, sbyte>.Convert(value);
    }
}
