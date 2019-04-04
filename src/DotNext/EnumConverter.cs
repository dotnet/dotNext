using System;

namespace DotNext
{
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
        /// <returns>Enum value equal to the given <see cref="long"/> value.</returns>
        public static T ToEnum<T>(this long value) where T : unmanaged, Enum => value.BitCast<long, T>();

        /// <summary>
        /// Converts <see cref="int"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equal to the given <see cref="int"/> value.</returns>
        public static T ToEnum<T>(this int value) where T : unmanaged, Enum => value.BitCast<int, T>();

        /// <summary>
        /// Converts <see cref="short"/> into enum of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target enum type.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Enum value equal to the given <see cref="short"/> value.</returns>
        public static T ToEnum<T>(this short value) where T : unmanaged, Enum => value.BitCast<short, T>();

        public static T ToEnum<T>(this byte value) where T : unmanaged, Enum => value.BitCast<byte, T>();

        [CLSCompliant(false)]
        public static T ToEnum<T>(this sbyte value) where T : unmanaged, Enum => value.BitCast<sbyte, T>();

        [CLSCompliant(false)]
        public static T ToEnum<T>(this ushort value) where T : unmanaged, Enum => value.BitCast<ushort, T>();

        [CLSCompliant(false)]
        public static T ToEnum<T>(this uint value) where T : unmanaged, Enum => value.BitCast<uint, T>();

        [CLSCompliant(false)]
        public static T ToEnum<T>(this ulong value) where T : unmanaged, Enum => value.BitCast<ulong, T>();

        public static long ToInt64<T>(this T value) where T : struct, Enum => ValueTypeExtensions.ToInt64(value);

        public static int ToInt32<T>(this T value) where T : struct, Enum => ValueTypeExtensions.ToInt32(value);

        public static short ToInt16<T>(this T value) where T : struct, Enum => ValueTypeExtensions.ToInt16(value);

        public static byte ToByte<T>(this T value) where T : struct, Enum => ValueTypeExtensions.ToByte(value);

        [CLSCompliant(false)]
        public static ulong ToUInt64<T>(this T value) where T : struct, Enum => ValueTypeExtensions.ToUInt64(value);

        [CLSCompliant(false)]
        public static uint ToUInt32<T>(this T value) where T : struct, Enum => ValueTypeExtensions.ToUInt32(value);

        [CLSCompliant(false)]
        public static ushort ToUInt16<T>(this T value) where T : struct, Enum => ValueTypeExtensions.ToUInt16(value);

        [CLSCompliant(false)]
        public static sbyte ToSByte<T>(this T value) where T : struct, Enum => ValueTypeExtensions.ToSByte(value);
    }
}
