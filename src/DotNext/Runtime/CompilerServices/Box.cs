using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Typed representation of the boxed value type.
    /// </summary>
    /// <typeparam name="T">The value type to be boxed.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Box<T> : IEquatable<Box<T>> // TODO: Move to Runtime namespace
        where T : struct
    {
        private readonly object value;

        /// <summary>
        /// Wraps the reference to the boxed value type.
        /// </summary>
        /// <param name="boxed">An object representing boxed value type of type <typeparamref name="T"/>.</param>
        /// <exception cref="ArgumentException"><paramref name="boxed"/> is not of type <typeparamref name="T"/>.</exception>
        public Box(object boxed) => value = boxed is T ? boxed : throw new ArgumentException(ExceptionMessages.BoxedValueTypeExpected<T>(), nameof(boxed));

        /// <summary>
        /// Creates boxed representation of the specified value type.
        /// </summary>
        /// <param name="value">The value to box.</param>
        public Box(T value) => this.value = value;

        /// <summary>
        /// Indicates that this container hold no reference.
        /// </summary>
        public bool IsEmpty => value is null;

        /// <summary>
        /// Gets a managed pointer to the boxed value type.
        /// </summary>
        /// <value>The managed pointer to the boxed value type.</value>
        /// <seealso cref="Unsafe.Unbox{T}"/>
        public ref T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.Unbox<T>(value);
        }

        /// <summary>
        /// Gets pinnable managed pointer to the boxed object.
        /// </summary>
        /// <returns>The pinnable managed pointer.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ref T GetPinnableReference() => ref Value;

        /// <summary>
        /// Unboxes the reference value.
        /// </summary>
        /// <param name="box">The boxed representation of value type.</param>
        /// <returns>Unboxed value type.</returns>
        public static explicit operator T(Box<T> box) => box.Value;

        /// <summary>
        /// Determines whether this container holds the same reference as the specified container.
        /// </summary>
        /// <param name="other">The other container to compare.</param>
        /// <returns><see langword="true"/> his container holds the same reference as the specified container; otherwise, <see langword="false"/>.</returns>
        public bool Equals(Box<T> other) => ReferenceEquals(value, other.value);

        /// <summary>
        /// Determines whether this container holds the same reference as the specified container.
        /// </summary>
        /// <param name="other">The other container to compare.</param>
        /// <returns><see langword="true"/> his container holds the same reference as the specified container; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is Box<T> box && Equals(box);

        /// <summary>
        /// Returns hash code of the reference stored in this container.
        /// </summary>
        /// <returns>The hash code of the reference to the boxed value.</returns>
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(value);

        /// <summary>
        /// Converts stored reference to the string.
        /// </summary>
        /// <returns>The textual representation of the stored reference.</returns>
        public override string ToString() => value?.ToString() ?? string.Empty;

        /// <summary>
        /// Determines whether the two containers store the references to the same boxed value.
        /// </summary>
        /// <param name="x">The first container to compare.</param>
        /// <param name="y">The second container to compare.</param>
        /// <returns><see langword="true"/> if both containers store the references to the same boxed value; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(Box<T> x, Box<T> y) => ReferenceEquals(x.value, y.value);

        /// <summary>
        /// Determines whether the two containers store the references to the different boxed values.
        /// </summary>
        /// <param name="x">The first container to compare.</param>
        /// <param name="y">The second container to compare.</param>
        /// <returns><see langword="true"/> if both containers store the references to the different boxed values; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(Box<T> x, Box<T> y) => !ReferenceEquals(x.value, y.value);
    }
}