using System;
using System.Dynamic;
using Expression = System.Linq.Expressions.Expression;

namespace DotNext.VariantType
{
    /// <summary>
    /// Represents value that can be one of two possible types.
    /// </summary>
    /// <remarks>
    /// Variant data type is fully compatible with <see langword="dynamic"/>
    /// keyword and late binding.
    /// </remarks>
    /// <typeparam name="T1">First possible type.</typeparam>
    /// <typeparam name="T2">Second possible type.</typeparam>
    public readonly struct Variant<T1, T2> : IEquatable<Variant<T1, T2>>, IVariant
        where T1 : class
        where T2 : class
    {
        private readonly object Value;

        private Variant(object value) => Value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T1"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
		public Variant(T1 value) => Value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T2"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
        public Variant(T2 value) => Value = value;

        /// <summary>
        /// Indicates that this container stores non-<see langword="null"/> value.
        /// </summary>
        public bool IsPresent => !(Value is null);

        object IVariant.Value => Value;

        /// <summary>
        /// Interprets stored value as <typeparamref name="T1"/>.
        /// </summary>
        public Optional<T1> First => (Value as T1).EmptyIfNull();

        /// <summary>
        /// Interprets stored value as <typeparamref name="T2"/>.
        /// </summary>
        public Optional<T2> Second => (Value as T2).EmptyIfNull();

        /// <summary>
        /// Converts the stored value.
        /// </summary>
        /// <typeparam name="R">The type of conversion result.</typeparam>
        /// <param name="mapper1">The converter for the first possible type.</param>
        /// <param name="mapper2">The converter for the second possible type.</param>
        /// <returns>Conversion result; or <see cref="Optional{T}.Empty"/> if stored value is <see langword="null"/>.</returns>
        public Optional<R> Convert<R>(Converter<T1, R> mapper1, Converter<T2, R> mapper2)
        {
            switch (Value)
            {
                case T1 first: return mapper1(first);
                case T2 second: return mapper2(second);
                default: return Optional<R>.Empty;
            }
        }

        /// <summary>
        /// Converts this variant value into another value.
        /// </summary>
        /// <typeparam name="U1">The first possible type of the conversion result.</typeparam>
        /// <typeparam name="U2">The second possible type of the conversion result.</typeparam>
        /// <param name="mapper1">The converter for the first possible type.</param>
        /// <param name="mapper2">The converter for the second possible type.</param>
        /// <returns>The variant value converted from this variant value.</returns>
        public Variant<U1, U2> Convert<U1, U2>(Converter<T1, U1> mapper1, Converter<T2, U2> mapper2)
            where U1 : class
            where U2 : class
        {
            switch (Value)
            {
                case T1 first: return new Variant<U1, U2>(mapper1(first));
                case T2 second: return new Variant<U1, U2>(mapper2(second));
                default: return default;
            }
        }

        /// <summary>
        /// Change order of type parameters.
        /// </summary>
        /// <returns>A copy of variant value with changed order of type parameters.</returns>
        public Variant<T2, T1> Permute() => new Variant<T2, T1>(Value);

        /// <summary>
        /// Converts value of type <typeparamref name="T1"/> into variant.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        public static implicit operator Variant<T1, T2>(T1 value) => new Variant<T1, T2>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T1"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T1"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T1"/>.</param>
        public static explicit operator T1(Variant<T1, T2> var) => var.Value as T1;

        /// <summary>
        /// Converts value of type <typeparamref name="T2"/> into variant.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        public static implicit operator Variant<T1, T2>(T2 value) => new Variant<T1, T2>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T2"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T2"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T2"/>.</param>
        public static explicit operator T2(Variant<T1, T2> var) => var.Value as T2;

        /// <summary>
        /// Determines whether the value stored in this variant
        /// container is equal to the value stored in the given variant
        /// container.
        /// </summary>
        /// <typeparam name="V">The type of variant container.</typeparam>
        /// <param name="other">Other variant value to compare.</param>
        /// <returns>
        /// <see langword="true"/>, if the value stored in this variant
        /// container is equal to the value stored in the given variant
        /// container; otherwise, <see langword="false"/>.
        /// </returns>
        public bool Equals<V>(V other)
            where V : IVariant
            => Equals(Value, other.Value);

        bool IEquatable<Variant<T1, T2>>.Equals(Variant<T1, T2> other) => Equals(other);

        /// <summary>
        /// Determines whether the two variant values are equal.
        /// </summary>
        /// <remarks>
        /// This operator uses <see cref="object.Equals(object, object)"/>
        /// to compare stored values.
        /// </remarks>
        /// <param name="first">The first value to compare.</param>
        /// <param name="second">The second value to compare.</param>
        /// <returns><see langword="true"/>, if variant values are equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(Variant<T1, T2> first, Variant<T1, T2> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the two variant values are not equal.
        /// </summary>
        /// <remarks>
        /// This operator uses <see cref="object.Equals(object, object)"/>
        /// to compare stored values.
        /// </remarks>
        /// <param name="first">The first value to compare.</param>
        /// <param name="second">The second value to compare.</param>
        /// <returns><see langword="true"/>, if variant values are not equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(Variant<T1, T2> first, Variant<T1, T2> second) => !first.Equals(second);

        /// <summary>
        /// Indicates that variant value is non-<see langword="null"/> value.
        /// </summary>
		public static bool operator true(Variant<T1, T2> variant) => !(variant.Value is null);

        /// <summary>
        /// Indicates that variant value is <see langword="null"/> value.
        /// </summary>
		public static bool operator false(Variant<T1, T2> variant) => variant.Value is null;

        /// <summary>
        /// Provides textual representation of the stored value.
        /// </summary>
        /// <remarks>
        /// This method calls virtual method <see cref="object.ToString()"/>
        /// for the stored value.
        /// </remarks>
        /// <returns>The textual representation of the stored value.</returns>
        public override string ToString() => Value?.ToString() ?? "";

        /// <summary>
        /// Computes hash code for the stored value.
        /// </summary>
        /// <returns>The hash code of the stored value.</returns>
        public override int GetHashCode() => Value is null ? 0 : Value.GetHashCode();

        /// <summary>
        /// Determines whether stored value is equal to the given value.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if stored value is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other)
            => other is IVariant variant ? Equals(Value, variant.Value) : Equals(Value, other);

        DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
            => new VariantImmutableMetaObject(parameter, this);
    }

    /// <summary>
    /// Represents value that can be one of three possible types.
    /// </summary>
    /// <remarks>
    /// Variant data type is fully compatible with <see langword="dynamic"/>
    /// keyword and late binding.
    /// </remarks>
    /// <typeparam name="T1">First possible type.</typeparam>
    /// <typeparam name="T2">Second possible type.</typeparam>
    /// <typeparam name="T3">Third possible type.</typeparam>
    public readonly struct Variant<T1, T2, T3> : IVariant, IEquatable<Variant<T1, T2, T3>>
        where T1 : class
        where T2 : class
        where T3 : class
    {
        private readonly object Value;

        private Variant(object value) => Value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T1"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
        public Variant(T1 value) => Value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T2"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
        public Variant(T2 value) => Value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T3"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
        public Variant(T3 value) => Value = value;

        private static Variant<T1, T2, T3> Create<V>(V variant)
            where V : struct, IVariant
            => new Variant<T1, T2, T3>(variant.Value);

        /// <summary>
        /// Indicates that this container stores non-<see langword="null"/> value.
        /// </summary>
        public bool IsPresent => !(Value is null);

        /// <summary>
        /// Change order of type parameters.
        /// </summary>
        /// <returns>A copy of variant value with changed order of type parameters.</returns>
        public Variant<T3, T1, T2> Permute() => new Variant<T3, T1, T2>(Value);

        object IVariant.Value => Value;

        /// <summary>
        /// Determines whether the value stored in this variant
        /// container is equal to the value stored in the given variant
        /// container.
        /// </summary>
        /// <typeparam name="V">The type of variant container.</typeparam>
        /// <param name="other">Other variant value to compare.</param>
        /// <returns>
        /// <see langword="true"/>, if the value stored in this variant
        /// container is equal to the value stored in the given variant
        /// container; otherwise, <see langword="false"/>.
        /// </returns>
        public bool Equals<V>(V other)
            where V : IVariant
            => Equals(Value, other.Value);

        bool IEquatable<Variant<T1, T2, T3>>.Equals(Variant<T1, T2, T3> other) => Equals(other);

        /// <summary>
        /// Determines whether the two variant values are equal.
        /// </summary>
        /// <remarks>
        /// This operator uses <see cref="object.Equals(object, object)"/>
        /// to compare stored values.
        /// </remarks>
        /// <param name="first">The first value to compare.</param>
        /// <param name="second">The second value to compare.</param>
        /// <returns><see langword="true"/>, if variant values are equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(Variant<T1, T2, T3> first, Variant<T1, T2, T3> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the two variant values are not equal.
        /// </summary>
        /// <remarks>
        /// This operator uses <see cref="object.Equals(object, object)"/>
        /// to compare stored values.
        /// </remarks>
        /// <param name="first">The first value to compare.</param>
        /// <param name="second">The second value to compare.</param>
        /// <returns><see langword="true"/>, if variant values are not equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(Variant<T1, T2, T3> first, Variant<T1, T2, T3> second) => !first.Equals(second);

        /// <summary>
        /// Converts value of type <typeparamref name="T1"/> into variant.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        public static implicit operator Variant<T1, T2, T3>(T1 value) => new Variant<T1, T2, T3>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T1"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T1"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T1"/>.</param>
        public static explicit operator T1(Variant<T1, T2, T3> var) => var.Value as T1;

        /// <summary>
        /// Converts value of type <typeparamref name="T2"/> into variant.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        public static implicit operator Variant<T1, T2, T3>(T2 value) => new Variant<T1, T2, T3>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T2"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T2"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T2"/>.</param>
        public static explicit operator T2(Variant<T1, T2, T3> var) => var.Value as T2;

        /// <summary>
        /// Converts value of type <typeparamref name="T3"/> into variant.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        public static implicit operator Variant<T1, T2, T3>(T3 value) => new Variant<T1, T2, T3>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T3"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T3"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T3"/>.</param>
        public static explicit operator T3(Variant<T1, T2, T3> var) => var.Value as T3;

        /// <summary>
        /// Converts variant value of two possible types into variant value
        /// of three possibles types.
        /// </summary>
        /// <param name="variant">The variant value to be converted.</param>
        public static implicit operator Variant<T1, T2, T3>(Variant<T1, T2> variant)
            => Create(variant);

        /// <summary>
        /// Indicates that variant value is non-<see langword="null"/> value.
        /// </summary>
		public static bool operator true(Variant<T1, T2, T3> variant) => !(variant.Value is null);

        /// <summary>
        /// Indicates that variant value is <see langword="null"/> value.
        /// </summary>
		public static bool operator false(Variant<T1, T2, T3> variant) => variant.Value is null;

        /// <summary>
        /// Provides textual representation of the stored value.
        /// </summary>
        /// <remarks>
        /// This method calls virtual method <see cref="object.ToString()"/>
        /// for the stored value.
        /// </remarks>
        /// <returns>The textual representation of the stored value.</returns>
        public override string ToString() => Value?.ToString() ?? "";

        /// <summary>
        /// Computes hash code for the stored value.
        /// </summary>
        /// <returns>The hash code of the stored value.</returns>
        public override int GetHashCode() => Value is null ? 0 : Value.GetHashCode();

        /// <summary>
        /// Determines whether stored value is equal to the given value.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if stored value is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other)
            => other is IVariant variant ? Equals(Value, variant.Value) : Equals(Value, other);

        DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
            => new VariantImmutableMetaObject(parameter, this);
    }

    /// <summary>
	/// Represents value that can be one of three possible types.
	/// </summary>
    /// <remarks>
    /// Variant data type is fully compatible with <see langword="dynamic"/>
    /// keyword and late binding.
    /// </remarks>
	/// <typeparam name="T1">First possible type.</typeparam>
	/// <typeparam name="T2">Second possible type.</typeparam>
	/// <typeparam name="T3">Third possible type.</typeparam>
    /// <typeparam name="T4">Fourth possible type.</typeparam>
	public readonly struct Variant<T1, T2, T3, T4> : IVariant, IEquatable<Variant<T1, T2, T3, T4>>
        where T1 : class
        where T2 : class
        where T3 : class
        where T4 : class
    {
        private readonly object Value;

        private Variant(object value) => Value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T1"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
        public Variant(T1 value) => Value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T2"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
        public Variant(T2 value) => Value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T3"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
        public Variant(T3 value) => Value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T4"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
        public Variant(T4 value) => Value = value;

        private static Variant<T1, T2, T3, T4> Create<V>(V variant)
            where V : struct, IVariant
            => new Variant<T1, T2, T3, T4>(variant.Value);

        /// <summary>
        /// Indicates that this container stores non-<see langword="null"/> value.
        /// </summary>
        public bool IsPresent => !(Value is null);

        /// <summary>
        /// Change order of type parameters.
        /// </summary>
        /// <returns>A copy of variant value with changed order of type parameters.</returns>
        public Variant<T4, T1, T2, T3> Permute() => new Variant<T4, T1, T2, T3>(Value);

        object IVariant.Value => Value;

        /// <summary>
        /// Determines whether the value stored in this variant
        /// container is equal to the value stored in the given variant
        /// container.
        /// </summary>
        /// <typeparam name="V">The type of variant container.</typeparam>
        /// <param name="other">Other variant value to compare.</param>
        /// <returns>
        /// <see langword="true"/>, if the value stored in this variant
        /// container is equal to the value stored in the given variant
        /// container; otherwise, <see langword="false"/>.
        /// </returns>
        public bool Equals<V>(V other)
            where V : IVariant
            => Equals(Value, other.Value);

        bool IEquatable<Variant<T1, T2, T3, T4>>.Equals(Variant<T1, T2, T3, T4> other) => Equals(Value, other.Value);

        /// <summary>
        /// Determines whether the two variant values are equal.
        /// </summary>
        /// <remarks>
        /// This operator uses <see cref="object.Equals(object, object)"/>
        /// to compare stored values.
        /// </remarks>
        /// <param name="first">The first value to compare.</param>
        /// <param name="second">The second value to compare.</param>
        /// <returns><see langword="true"/>, if variant values are equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(Variant<T1, T2, T3, T4> first, Variant<T1, T2, T3, T4> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the two variant values are not equal.
        /// </summary>
        /// <remarks>
        /// This operator uses <see cref="object.Equals(object, object)"/>
        /// to compare stored values.
        /// </remarks>
        /// <param name="first">The first value to compare.</param>
        /// <param name="second">The second value to compare.</param>
        /// <returns><see langword="true"/>, if variant values are not equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(Variant<T1, T2, T3, T4> first, Variant<T1, T2, T3, T4> second) => !first.Equals(second);

        /// <summary>
        /// Converts value of type <typeparamref name="T1"/> into variant.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        public static implicit operator Variant<T1, T2, T3, T4>(T1 value) => new Variant<T1, T2, T3, T4>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T2"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T1"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T1"/>.</param>
        public static explicit operator T1(Variant<T1, T2, T3, T4> var) => var.Value as T1;

        /// <summary>
        /// Converts value of type <typeparamref name="T2"/> into variant.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        public static implicit operator Variant<T1, T2, T3, T4>(T2 value) => new Variant<T1, T2, T3, T4>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T2"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T2"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T2"/>.</param>
        public static explicit operator T2(Variant<T1, T2, T3, T4> var) => var.Value as T2;

        /// <summary>
        /// Converts value of type <typeparamref name="T3"/> into variant.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        public static implicit operator Variant<T1, T2, T3, T4>(T3 value) => new Variant<T1, T2, T3, T4>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T3"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T3"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T3"/>.</param>
        public static explicit operator T3(Variant<T1, T2, T3, T4> var) => var.Value as T3;

        /// <summary>
        /// Converts value of type <typeparamref name="T4"/> into variant.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        public static implicit operator Variant<T1, T2, T3, T4>(T4 value) => new Variant<T1, T2, T3, T4>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T4"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T4"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T4"/>.</param>
        public static explicit operator T4(Variant<T1, T2, T3, T4> var) => var.Value as T4;

        /// <summary>
        /// Converts variant value of three possible types into variant value
        /// of four possibles types.
        /// </summary>
        /// <param name="variant">The variant value to be converted.</param>
        public static implicit operator Variant<T1, T2, T3, T4>(Variant<T1, T2, T3> variant)
            => Create(variant);

        /// <summary>
        /// Converts variant value of two possible types into variant value
        /// of four possibles types.
        /// </summary>
        /// <param name="variant">The variant value to be converted.</param>
        public static implicit operator Variant<T1, T2, T3, T4>(Variant<T1, T2> variant)
            => Create(variant);

        /// <summary>
        /// Indicates that variant value is non-<see langword="null"/> value.
        /// </summary>
        public static bool operator true(Variant<T1, T2, T3, T4> variant) => !(variant.Value is null);

        /// <summary>
        /// Indicates that variant value is <see langword="null"/> value.
        /// </summary>
		public static bool operator false(Variant<T1, T2, T3, T4> variant) => variant.Value is null;

        /// <summary>
        /// Provides textual representation of the stored value.
        /// </summary>
        /// <remarks>
        /// This method calls virtual method <see cref="object.ToString()"/>
        /// for the stored value.
        /// </remarks>
        /// <returns>The textual representation of the stored value.</returns>
        public override string ToString() => Value?.ToString() ?? "";

        /// <summary>
        /// Computes hash code for the stored value.
        /// </summary>
        /// <returns>The hash code of the stored value.</returns>
        public override int GetHashCode() => Value is null ? 0 : Value.GetHashCode();

        /// <summary>
        /// Determines whether stored value is equal to the given value.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if stored value is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other)
            => other is IVariant variant ? Equals(Value, variant.Value) : Equals(Value, other);

        DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
            => new VariantImmutableMetaObject(parameter, this);
    }
}