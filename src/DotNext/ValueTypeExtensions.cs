using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DotNext
{
    /// <summary>
    /// Various extensions for value types.
    /// </summary>
    public static class ValueTypeExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string ToString<T>(T value, IFormatProvider provider = null) where T : struct, IConvertible => value.ToString(provider);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string ToString<T>(T value, string format, IFormatProvider provider = null) where T : struct, IFormattable => value.ToString(format, provider);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TypeCode GetTypeCode<T>() where T : struct, IConvertible => new T().GetTypeCode();

        /// <summary>
        /// Obtain a value of type <typeparamref name="To"/> by 
        /// reinterpreting the object representation of <typeparamref name="From"/>.
        /// </summary>
        /// <param name="input">A value to convert.</param>
        /// <param name="output">Conversion result.</param>
        /// <typeparam name="From">The type of input struct.</typeparam>
        /// <typeparam name="To">The type of output struct.</typeparam>
        /// <seealso cref="ValueType{T}.Bitcast{To}"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Bitcast<From, To>(this From input, out To output)
            where From : unmanaged
            where To : unmanaged
            => ValueType<From>.Bitcast(in input, out output);

        /// <summary>
        /// Obtain a value of type <typeparamref name="To"/> by 
        /// reinterpreting the object representation of <typeparamref name="From"/>. 
        /// </summary>
        /// <remarks>
        /// Every bit in the value representation of the returned <typeparamref name="To"/> object 
        /// is equal to the corresponding bit in the object representation of <typeparamref name="From"/>. 
        /// The values of padding bits in the returned <typeparamref name="To"/> object are unspecified. 
        /// The method takes into account size of <typeparamref name="From"/> and <typeparamref name="To"/> types
        /// and able to provide conversion between types of different size.
        /// </remarks>
        /// <param name="input">A value to convert.</param>
        /// <typeparam name="From">The type of input struct.</typeparam>
        /// <typeparam name="To">The type of output struct.</typeparam>
        /// <returns>Conversion result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static To Bitcast<From, To>(this From input)
            where From : unmanaged
            where To : unmanaged
        {
            ValueType<From>.Bitcast<To>(in input, out var output);
            return output;
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
		public static bool IsOneOf<T>(this T value, IEnumerable<T> values)
            where T : struct, IEquatable<T>
        {
            foreach (var v in values)
                if (v.Equals(value))
                    return true;
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
            => value.IsOneOf((IEnumerable<T>)values);

        /// <summary>
        /// Create boxed representation of the value type.
        /// </summary>
        /// <param name="value">Value to be placed into heap.</param>
        /// <typeparam name="T">Value type.</typeparam>
        /// <returns>Boxed representation of value type.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueType<T> Box<T>(this T value)
            where T : struct
            => new ValueType<T>(value);

        /// <summary>
        /// Create boxed representation of the nullable value type.
        /// </summary>
        /// <param name="value">Value to be placed into heap.</param>
        /// <typeparam name="T">Value type.</typeparam>
        /// <returns>Boxed representation of nullable value type; or <see langword="null"/>.</returns>        
        public static ValueType<T> Box<T>(this T? value)
            where T : struct
            => value.HasValue ? new ValueType<T>(value.Value) : null;

        /// <summary>
        /// Attempts to get value from nullable container.
        /// </summary>
        /// <typeparam name="T">The underlying value type of the nullable type.</typeparam>
        /// <param name="nullable">Nullable value.</param>
        /// <param name="value">Underlying value.</param>
        /// <returns><see langword="true"/> if <paramref name="nullable"/> is not <see langword="null"/>; otherwise, <see langword="false"/>.</returns>
        public static bool TryGet<T>(this T? nullable, out T value) where T : struct
        {
            value = nullable.GetValueOrDefault();
            return nullable.HasValue;
        }
    }
}