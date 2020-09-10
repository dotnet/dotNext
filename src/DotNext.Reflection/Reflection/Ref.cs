using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace DotNext.Reflection
{
    using static Runtime.Intrinsics;

    internal static class Ref
    {
        private static bool Is(Type type) => type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(Ref<>);

        internal static bool Reflect(Type byRefType, [NotNullWhen(true)]out Type? underlyingType, [NotNullWhen(true)]out FieldInfo? valueField)
        {
            if (Is(byRefType))
            {
                underlyingType = byRefType.GetGenericArguments()[0];
                valueField = byRefType.GetField(nameof(Ref<Missing>.Value), BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance);
                return true;
            }
            else
            {
                underlyingType = null;
                valueField = null;
                return false;
            }
        }
    }

    /// <summary>
    /// Wrapper for by-ref argument.
    /// </summary>
    /// <remarks>
    /// This type has special semantics when used as argument type
    /// for delegates <see cref="Function{T, A, R}"/>, <see cref="Function{A, R}"/>,
    /// <see cref="Procedure{T, A}"/>, <see cref="Procedure{A}"/> when
    /// using strongly typed reflection. Argument of this type
    /// means that it should be passed into reflected method or constructor
    /// by reference. In all other scenarios, including <see cref="Reflector.Unreflect{D}(ConstructorInfo)"/>
    /// or <see cref="Reflector.Unreflect{D}(MethodInfo)"/>, this type treated as regular value type
    /// without special semantics.
    /// </remarks>
    /// <typeparam name="T">Referenced type.</typeparam>
    [SecuritySafeCritical]
    [StructLayout(LayoutKind.Auto)]
    public struct Ref<T> : IStrongBox, IEquatable<Ref<T>>
    {
        /// <summary>
        /// Gets or sets value.
        /// </summary>
        [SuppressMessage("Design", "CA1051", Justification = "It is by-design due to nature of this type")]
        [AllowNull]
        public T Value;

        /// <inheritdoc/>
        object? IStrongBox.Value
        {
            get => Value;
            set => Value = (T)value!;
        }

        /// <summary>
        /// Extracts actual value from the reference.
        /// </summary>
        /// <param name="reference">Typed reference.</param>
        /// <returns>Dereferenced value.</returns>
        public static implicit operator T(in Ref<T> reference) => reference.Value;

        /// <summary>
        /// Obtains a reference to the value.
        /// </summary>
        /// <param name="value">A value.</param>
        /// <returns>A reference to a value.</returns>
        public static implicit operator Ref<T>(T value) => new Ref<T> { Value = value };

        /// <summary>
        /// Identifies that two references point to the same location.
        /// </summary>
        /// <param name="first">The first reference.</param>
        /// <param name="second">The second reference.</param>
        /// <returns>True, if both references are equal.</returns>
        public static bool operator ==(in Ref<T> first, in Ref<T> second)
            => AreSame(in first.Value, in second.Value);

        /// <summary>
        /// Identifies that two references point to different locations.
        /// </summary>
        /// <param name="first">The first reference.</param>
        /// <param name="second">The second reference.</param>
        /// <returns>True, if both references are not equal.</returns>
        public static bool operator !=(in Ref<T> first, in Ref<T> second)
            => !AreSame(in first.Value, in second.Value);

        /// <summary>
        /// Gets hash code of this reference based on its address.
        /// </summary>
        /// <returns>Hash code of the reference.</returns>
        public override int GetHashCode() => AddressOf(in Value).GetHashCode();

        /// <summary>
        /// Always returns <see langword="false"/> because two boxed references
        /// have different address.
        /// </summary>
        /// <remarks>
        /// Use equality operator instead.
        /// </remarks>
        /// <param name="other">Other object to compare.</param>
        /// <returns>Always <see langword="false"/>.</returns>
        public override bool Equals(object? other) => false;

        /// <inheritdoc/>
        bool IEquatable<Ref<T>>.Equals(Ref<T> other) => false;
    }
}