using System;
using System.Runtime.InteropServices;

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
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Variant<T1, T2> : IEquatable<Variant<T1, T2>>, IVariant
        where T1 : class
        where T2 : class
    {
        private readonly object? value;

        private Variant(object? value) => this.value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T1"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
        public Variant(T1? value) => this.value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T2"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
        public Variant(T2? value) => this.value = value;

        /// <summary>
        /// Indicates that this container stores non-<see langword="null"/> value.
        /// </summary>
        public bool IsNull => value is null;

        /// <inheritdoc/>
        object? IVariant.Value => value;

        /// <summary>
        /// Interprets stored value as <typeparamref name="T1"/>.
        /// </summary>
        public Optional<T1> First => value as T1;

        /// <summary>
        /// Interprets stored value as <typeparamref name="T2"/>.
        /// </summary>
        public Optional<T2> Second => value as T2;

        private Optional<TResult> Convert<TResult, TConverter1, TConverter2>(TConverter1 mapper1, TConverter2 mapper2)
            where TConverter1 : struct, ISupplier<T1, TResult>
            where TConverter2 : struct, ISupplier<T2, TResult>
            => value switch
            {
                T1 first => mapper1.Invoke(first),
                T2 second => mapper2.Invoke(second),
                _ => Optional<TResult>.None,
            };

        /// <summary>
        /// Converts the stored value.
        /// </summary>
        /// <typeparam name="TResult">The type of conversion result.</typeparam>
        /// <param name="mapper1">The converter for the first possible type.</param>
        /// <param name="mapper2">The converter for the second possible type.</param>
        /// <returns>Conversion result; or <see cref="Optional{T}.None"/> if stored value is <see langword="null"/>.</returns>
        public Optional<TResult> Convert<TResult>(Converter<T1, TResult> mapper1, Converter<T2, TResult> mapper2)
            => Convert<TResult, DelegatingConverter<T1, TResult>, DelegatingConverter<T2, TResult>>(mapper1, mapper2);

        /// <summary>
        /// Converts the stored value.
        /// </summary>
        /// <typeparam name="TResult">The type of conversion result.</typeparam>
        /// <param name="mapper1">The converter for the first possible type.</param>
        /// <param name="mapper2">The converter for the second possible type.</param>
        /// <returns>Conversion result; or <see cref="Optional{T}.None"/> if stored value is <see langword="null"/>.</returns>
        [CLSCompliant(false)]
        public unsafe Optional<TResult> Convert<TResult>(delegate*<T1, TResult> mapper1, delegate*<T2, TResult> mapper2)
            => Convert<TResult, Supplier<T1, TResult>, Supplier<T2, TResult>>(mapper1, mapper2);

        private Variant<TResult1, TResult2> Convert<TResult1, TResult2, TConverter1, TConverter2>(TConverter1 mapper1, TConverter2 mapper2)
            where TResult1 : class
            where TResult2 : class
            where TConverter1 : struct, ISupplier<T1, TResult1>
            where TConverter2 : struct, ISupplier<T2, TResult2>
            => value switch
            {
                T1 first => new Variant<TResult1, TResult2>(mapper1.Invoke(first)),
                T2 second => new Variant<TResult1, TResult2>(mapper2.Invoke(second)),
                _ => default,
            };

        /// <summary>
        /// Converts this variant value into another value.
        /// </summary>
        /// <typeparam name="TResult1">The first possible type of the conversion result.</typeparam>
        /// <typeparam name="TResult2">The second possible type of the conversion result.</typeparam>
        /// <param name="mapper1">The converter for the first possible type.</param>
        /// <param name="mapper2">The converter for the second possible type.</param>
        /// <returns>The variant value converted from this variant value.</returns>
        public Variant<TResult1, TResult2> Convert<TResult1, TResult2>(Converter<T1, TResult1> mapper1, Converter<T2, TResult2> mapper2)
            where TResult1 : class
            where TResult2 : class
            => Convert<TResult1, TResult2, DelegatingConverter<T1, TResult1>, DelegatingConverter<T2, TResult2>>(mapper1, mapper2);

        /// <summary>
        /// Converts this variant value into another value.
        /// </summary>
        /// <typeparam name="TResult1">The first possible type of the conversion result.</typeparam>
        /// <typeparam name="TResult2">The second possible type of the conversion result.</typeparam>
        /// <param name="mapper1">The converter for the first possible type.</param>
        /// <param name="mapper2">The converter for the second possible type.</param>
        /// <returns>The variant value converted from this variant value.</returns>
        [CLSCompliant(false)]
        public unsafe Variant<TResult1, TResult2> Convert<TResult1, TResult2>(delegate*<T1, TResult1> mapper1, delegate*<T2, TResult2> mapper2)
            where TResult1 : class
            where TResult2 : class
            => Convert<TResult1, TResult2, Supplier<T1, TResult1>, Supplier<T2, TResult2>>(mapper1, mapper2);

        /// <summary>
        /// Change order of type parameters.
        /// </summary>
        /// <returns>A copy of variant value with changed order of type parameters.</returns>
        public Variant<T2, T1> Permute() => new Variant<T2, T1>(value);

        /// <summary>
        /// Deconstructs this object.
        /// </summary>
        /// <remarks>
        /// This method called implicitly by deconstruction expression
        /// or positional pattern matching.
        /// </remarks>
        /// <param name="value1">The value of type <typeparamref name="T1"/>; or <see langword="null"/>.</param>
        /// <param name="value2">The value of type <typeparamref name="T2"/>; or <see langword="null"/>.</param>
        public void Deconstruct(out T1? value1, out T2? value2)
        {
            value1 = value as T1;
            value2 = value as T2;
        }

        /// <summary>
        /// Converts value of type <typeparamref name="T1"/> into variant.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        public static implicit operator Variant<T1, T2>(T1? value) => new Variant<T1, T2>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T1"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T1"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T1"/>.</param>
        public static explicit operator T1?(Variant<T1, T2> var) => var.value as T1;

        /// <summary>
        /// Converts value of type <typeparamref name="T2"/> into variant.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        public static implicit operator Variant<T1, T2>(T2? value) => new Variant<T1, T2>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T2"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T2"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T2"/>.</param>
        public static explicit operator T2?(Variant<T1, T2> var) => var.value as T2;

        /// <summary>
        /// Determines whether the value stored in this variant
        /// container is equal to the value stored in the given variant
        /// container.
        /// </summary>
        /// <typeparam name="TOther">The type of variant container.</typeparam>
        /// <param name="other">Other variant value to compare.</param>
        /// <returns>
        /// <see langword="true"/>, if the value stored in this variant
        /// container is equal to the value stored in the given variant
        /// container; otherwise, <see langword="false"/>.
        /// </returns>
        public bool Equals<TOther>(TOther other)
            where TOther : IVariant
            => Equals(value, other.Value);

        /// <inheritdoc/>
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
        /// <param name="variant">The variant value to convert.</param>
        public static bool operator true(Variant<T1, T2> variant) => variant.value is not null;

        /// <summary>
        /// Indicates that variant value is <see langword="null"/> value.
        /// </summary>
        /// <param name="variant">The variant value to convert.</param>
        public static bool operator false(Variant<T1, T2> variant) => variant.value is null;

        /// <summary>
        /// Provides textual representation of the stored value.
        /// </summary>
        /// <remarks>
        /// This method calls virtual method <see cref="object.ToString()"/>
        /// for the stored value.
        /// </remarks>
        /// <returns>The textual representation of the stored value.</returns>
        public override string ToString() => value?.ToString() ?? string.Empty;

        /// <summary>
        /// Computes hash code for the stored value.
        /// </summary>
        /// <returns>The hash code of the stored value.</returns>
        public override int GetHashCode() => value is null ? 0 : value.GetHashCode();

        /// <summary>
        /// Determines whether stored value is equal to the given value.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if stored value is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other)
            => other is IVariant variant ? Equals(value, variant.Value) : Equals(value, other);
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
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Variant<T1, T2, T3> : IVariant, IEquatable<Variant<T1, T2, T3>>
        where T1 : class
        where T2 : class
        where T3 : class
    {
        private readonly object? value;

        private Variant(object? value) => this.value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T1"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
        public Variant(T1? value) => this.value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T2"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
        public Variant(T2? value) => this.value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T3"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
        public Variant(T3? value) => this.value = value;

        private static Variant<T1, T2, T3> Create<TVariant>(TVariant variant)
            where TVariant : struct, IVariant
            => new Variant<T1, T2, T3>(variant.Value);

        /// <summary>
        /// Indicates that this container stores non-<see langword="null"/> value.
        /// </summary>
        public bool IsNull => value is null;

        /// <summary>
        /// Change order of type parameters.
        /// </summary>
        /// <returns>A copy of variant value with changed order of type parameters.</returns>
        public Variant<T3, T1, T2> Permute() => new Variant<T3, T1, T2>(value);

        /// <summary>
        /// Deconstructs this object.
        /// </summary>
        /// <remarks>
        /// This method called implicitly by deconstruction expression
        /// or positional pattern matching.
        /// </remarks>
        /// <param name="value1">The value of type <typeparamref name="T1"/>; or <see langword="null"/>.</param>
        /// <param name="value2">The value of type <typeparamref name="T2"/>; or <see langword="null"/>.</param>
        /// <param name="value3">The value of type <typeparamref name="T3"/>; or <see langword="null"/>.</param>
        public void Deconstruct(out T1? value1, out T2? value2, out T3? value3)
        {
            value1 = value as T1;
            value2 = value as T2;
            value3 = value as T3;
        }

        /// <inheritdoc/>
        object? IVariant.Value => value;

        /// <summary>
        /// Determines whether the value stored in this variant
        /// container is equal to the value stored in the given variant
        /// container.
        /// </summary>
        /// <typeparam name="TOther">The type of variant container.</typeparam>
        /// <param name="other">Other variant value to compare.</param>
        /// <returns>
        /// <see langword="true"/>, if the value stored in this variant
        /// container is equal to the value stored in the given variant
        /// container; otherwise, <see langword="false"/>.
        /// </returns>
        public bool Equals<TOther>(TOther other)
            where TOther : IVariant
            => Equals(value, other.Value);

        /// <inheritdoc/>
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
        public static implicit operator Variant<T1, T2, T3>(T1? value) => new Variant<T1, T2, T3>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T1"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T1"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T1"/>.</param>
        public static explicit operator T1?(Variant<T1, T2, T3> var) => var.value as T1;

        /// <summary>
        /// Converts value of type <typeparamref name="T2"/> into variant.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        public static implicit operator Variant<T1, T2, T3>(T2? value) => new Variant<T1, T2, T3>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T2"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T2"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T2"/>.</param>
        public static explicit operator T2?(Variant<T1, T2, T3> var) => var.value as T2;

        /// <summary>
        /// Converts value of type <typeparamref name="T3"/> into variant.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        public static implicit operator Variant<T1, T2, T3>(T3? value) => new Variant<T1, T2, T3>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T3"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T3"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T3"/>.</param>
        public static explicit operator T3?(Variant<T1, T2, T3> var) => var.value as T3;

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
        /// <param name="variant">The variant value to check.</param>
        public static bool operator true(Variant<T1, T2, T3> variant) => variant.value is not null;

        /// <summary>
        /// Indicates that variant value is <see langword="null"/> value.
        /// </summary>
        /// <param name="variant">The variant value to check.</param>
        public static bool operator false(Variant<T1, T2, T3> variant) => variant.value is null;

        /// <summary>
        /// Provides textual representation of the stored value.
        /// </summary>
        /// <remarks>
        /// This method calls virtual method <see cref="object.ToString()"/>
        /// for the stored value.
        /// </remarks>
        /// <returns>The textual representation of the stored value.</returns>
        public override string ToString() => value?.ToString() ?? string.Empty;

        /// <summary>
        /// Computes hash code for the stored value.
        /// </summary>
        /// <returns>The hash code of the stored value.</returns>
        public override int GetHashCode() => value is null ? 0 : value.GetHashCode();

        /// <summary>
        /// Determines whether stored value is equal to the given value.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if stored value is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other)
            => other is IVariant variant ? Equals(value, variant.Value) : Equals(value, other);
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
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Variant<T1, T2, T3, T4> : IVariant, IEquatable<Variant<T1, T2, T3, T4>>
        where T1 : class
        where T2 : class
        where T3 : class
        where T4 : class
    {
        private readonly object? value;

        private Variant(object? value) => this.value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T1"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
        public Variant(T1? value) => this.value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T2"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
        public Variant(T2? value) => this.value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T3"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
        public Variant(T3? value) => this.value = value;

        /// <summary>
        /// Creates a new variant value from value of type <typeparamref name="T4"/>.
        /// </summary>
        /// <param name="value">The value to be placed into variant container.</param>
        public Variant(T4? value) => this.value = value;

        private static Variant<T1, T2, T3, T4> Create<TVariant>(TVariant variant)
            where TVariant : struct, IVariant
            => new Variant<T1, T2, T3, T4>(variant.Value);

        /// <summary>
        /// Indicates that this container stores non-<see langword="null"/> value.
        /// </summary>
        public bool IsNull => value is null;

        /// <summary>
        /// Change order of type parameters.
        /// </summary>
        /// <returns>A copy of variant value with changed order of type parameters.</returns>
        public Variant<T4, T1, T2, T3> Permute() => new Variant<T4, T1, T2, T3>(value);

        /// <summary>
        /// Deconstructs this object.
        /// </summary>
        /// <remarks>
        /// This method called implicitly by deconstruction expression
        /// or positional pattern matching.
        /// </remarks>
        /// <param name="value1">The value of type <typeparamref name="T1"/>; or <see langword="null"/>.</param>
        /// <param name="value2">The value of type <typeparamref name="T2"/>; or <see langword="null"/>.</param>
        /// <param name="value3">The value of type <typeparamref name="T3"/>; or <see langword="null"/>.</param>
        /// <param name="value4">The value of type <typeparamref name="T4"/>; or <see langword="null"/>.</param>
        public void Deconstruct(out T1? value1, out T2? value2, out T3? value3, out T4? value4)
        {
            value1 = value as T1;
            value2 = value as T2;
            value3 = value as T3;
            value4 = value as T4;
        }

        /// <inheritdoc/>
        object? IVariant.Value => value;

        /// <summary>
        /// Determines whether the value stored in this variant
        /// container is equal to the value stored in the given variant
        /// container.
        /// </summary>
        /// <typeparam name="TOther">The type of variant container.</typeparam>
        /// <param name="other">Other variant value to compare.</param>
        /// <returns>
        /// <see langword="true"/>, if the value stored in this variant
        /// container is equal to the value stored in the given variant
        /// container; otherwise, <see langword="false"/>.
        /// </returns>
        public bool Equals<TOther>(TOther other)
            where TOther : IVariant
            => Equals(value, other.Value);

        /// <inheritdoc/>
        bool IEquatable<Variant<T1, T2, T3, T4>>.Equals(Variant<T1, T2, T3, T4> other) => Equals(value, other.value);

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
        public static implicit operator Variant<T1, T2, T3, T4>(T1? value) => new Variant<T1, T2, T3, T4>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T2"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T1"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T1"/>.</param>
        public static explicit operator T1?(Variant<T1, T2, T3, T4> var) => var.value as T1;

        /// <summary>
        /// Converts value of type <typeparamref name="T2"/> into variant.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        public static implicit operator Variant<T1, T2, T3, T4>(T2? value) => new Variant<T1, T2, T3, T4>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T2"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T2"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T2"/>.</param>
        public static explicit operator T2?(Variant<T1, T2, T3, T4> var) => var.value as T2;

        /// <summary>
        /// Converts value of type <typeparamref name="T3"/> into variant.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        public static implicit operator Variant<T1, T2, T3, T4>(T3? value) => new Variant<T1, T2, T3, T4>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T3"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T3"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T3"/>.</param>
        public static explicit operator T3?(Variant<T1, T2, T3, T4> var) => var.value as T3;

        /// <summary>
        /// Converts value of type <typeparamref name="T4"/> into variant.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        public static implicit operator Variant<T1, T2, T3, T4>(T4? value) => new Variant<T1, T2, T3, T4>(value);

        /// <summary>
        /// Converts variant value into type <typeparamref name="T4"/>.
        /// </summary>
        /// <param name="var">Variant value to convert into type <typeparamref name="T4"/>; or <see langword="null"/> if current value is not of type <typeparamref name="T4"/>.</param>
        public static explicit operator T4?(Variant<T1, T2, T3, T4> var) => var.value as T4;

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
        /// <param name="variant">The variant value to check.</param>
        public static bool operator true(Variant<T1, T2, T3, T4> variant) => variant.value is not null;

        /// <summary>
        /// Indicates that variant value is <see langword="null"/> value.
        /// </summary>
        /// <param name="variant">The variant value to check.</param>
        public static bool operator false(Variant<T1, T2, T3, T4> variant) => variant.value is null;

        /// <summary>
        /// Provides textual representation of the stored value.
        /// </summary>
        /// <remarks>
        /// This method calls virtual method <see cref="object.ToString()"/>
        /// for the stored value.
        /// </remarks>
        /// <returns>The textual representation of the stored value.</returns>
        public override string ToString() => value?.ToString() ?? string.Empty;

        /// <summary>
        /// Computes hash code for the stored value.
        /// </summary>
        /// <returns>The hash code of the stored value.</returns>
        public override int GetHashCode() => value is null ? 0 : value.GetHashCode();

        /// <summary>
        /// Determines whether stored value is equal to the given value.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if stored value is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other)
            => other is IVariant variant ? Equals(value, variant.Value) : Equals(value, other);
    }
}