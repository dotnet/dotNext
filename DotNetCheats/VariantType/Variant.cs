using System;
using System.Dynamic;
using Expression = System.Linq.Expressions.Expression;

namespace MissingPieces.VariantType
{
	/// <summary>
	/// Represents value that can be one of two possible types.
	/// </summary>
	/// <typeparam name="T1">First possible type.</typeparam>
	/// <typeparam name="T2">Second possible type.</typeparam>
	public readonly struct Variant<T1, T2>: IEquatable<Variant<T1, T2>>, IVariant
        where T1: class
        where T2: class
    {
        internal readonly object Value;

        private Variant(object value) => Value = value;

		public Variant(T1 value) => Value = value;

		public Variant(T2 value) => Value = value;

		/// <summary>
		/// Indicates that this container holds null value.
		/// </summary>
		public bool IsNull => Value is null;

		bool IOptional.IsPresent => !(Value is null);

		object IVariant.Value => Value;

		/// <summary>
		/// Interprets stored value as <typeparamref name="T1"/>.
		/// </summary>
        public Optional<T1> First => (Value as T1).EmptyIfNull();

		/// <summary>
		/// Interprets stored value as <typeparamref name="T2"/>.
		/// </summary>
        public Optional<T2> Second => (Value as T2).EmptyIfNull();

        public Optional<R> Map<R>(Func<T1, R> mapper1, Func<T2, R> mapper2)
        {
            switch(Value)
            {
                case T1 first: return mapper1(first);
                case T2 second: return mapper2(second);
                default: return Optional<R>.Empty;
            }
        }

        public Variant<U1, U2> Map<U1, U2>(Func<T1, U1> mapper1, Func<T2, U2> mapper2)
            where U1: class
            where U2: class
        {
            switch(Value)
            {
                case T1 first: return new Variant<U1, U2>(mapper1(first));
                case T2 second: return new Variant<U1, U2>(mapper2(second));
                default: return new Variant<U1, U2>();
            }
        }

        /// <summary>
        /// Change order of type parameters.
        /// </summary>
        /// <returns>A copy of variant value with changed order of type parameters.</returns>
        public Variant<T2, T1> Permute() => new Variant<T2, T1>(Value);

        public static implicit operator Variant<T1, T2>(T1 value) => new Variant<T1, T2>(value);
        public static explicit operator T1(Variant<T1, T2> var) => var.Value as T1;

        public static implicit operator Variant<T1, T2>(T2 value) => new Variant<T1, T2>(value);
        public static explicit operator T2(Variant<T1, T2> var) => var.Value as T2;

		public bool Equals(Variant<T1, T2> other) => Equals(Value, other.Value);

        public bool Equals(T1 other) => Equals(Value, other);

        public bool Equals(T2 other) => Equals(Value, other);

        public static bool operator==(Variant<T1, T2> first, T1 second) => first.Equals(second);
        public static bool operator!=(Variant<T1, T2> first, T1 second) => !first.Equals(second);

        public static bool operator==(Variant<T1, T2> first, T2 second) => first.Equals(second);
        public static bool operator!=(Variant<T1, T2> first, T2 second) => !first.Equals(second);

        public static bool operator==(Variant<T1, T2> first, Variant<T1, T2> second) => first.Equals(second);
        public static bool operator!=(Variant<T1, T2> first, Variant<T1, T2> second) => !first.Equals(second);

        public override string ToString() => Value?.ToString() ?? "";

        public override int GetHashCode() => Value is null ? 0: Value.GetHashCode();

        public override bool Equals(object other)
        {
            switch(other)
            {
                case IVariant variant: return Equals(Value, variant.Value);
                default: return Equals(Value, other);
            }
        }

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
			=> new VariantImmutableMetaObject(parameter, this);
    }

	/// <summary>
	/// Represents value that can be one of three possible types.
	/// </summary>
	/// <typeparam name="T1">First possible type.</typeparam>
	/// <typeparam name="T2">Second possible type.</typeparam>
	/// <typeparam name="T3">Third possible type.</typeparam>
	public readonly struct Variant<T1, T2, T3>: IVariant, IEquatable<Variant<T1, T2, T3>>
		where T1: class
		where T2: class
		where T3: class
	{
		internal readonly object Value;

		private Variant(object value)
			=> Value = value;

		public Variant(T1 value)
			=> Value = value;

		public Variant(T2 value)
			=> Value = value;

		public Variant(T3 value)
			=> Value = value;

		/// <summary>
		/// Indicates that this container holds null value.
		/// </summary>
		public bool IsNull => Value is null;

		bool IOptional.IsPresent => !(Value is null);

		/// <summary>
		/// Change order of type parameters.
		/// </summary>
		/// <returns>A copy of variant value with changed order of type parameters.</returns>
		public Variant<T3, T1, T2> Permute() => new Variant<T3, T1, T2>(Value);

		object IVariant.Value => Value;

		public bool Equals(Variant<T1, T2, T3> other) => Equals(Value, other.Value);

		public bool Equals(T1 other) => Equals(Value, other);

		public bool Equals(T2 other) => Equals(Value, other);

		public bool Equals(T3 other) => Equals(Value, other);

		public static bool operator ==(Variant<T1, T2, T3> first, T1 second) => first.Equals(second);
		public static bool operator !=(Variant<T1, T2, T3> first, T1 second) => !first.Equals(second);

		public static bool operator ==(Variant<T1, T2, T3> first, T2 second) => first.Equals(second);
		public static bool operator !=(Variant<T1, T2, T3> first, T2 second) => !first.Equals(second);

		public static bool operator ==(Variant<T1, T2, T3> first, T3 second) => first.Equals(second);
		public static bool operator !=(Variant<T1, T2, T3> first, T3 second) => !first.Equals(second);

		public static bool operator ==(Variant<T1, T2, T3> first, Variant<T1, T2, T3> second) => first.Equals(second);
		public static bool operator !=(Variant<T1, T2, T3> first, Variant<T1, T2, T3> second) => !first.Equals(second);

		public static implicit operator Variant<T1, T2, T3>(T1 value) => new Variant<T1, T2, T3>(value);
		public static explicit operator T1(Variant<T1, T2, T3> var) => var.Value as T1;

		public static implicit operator Variant<T1, T2, T3>(T2 value) => new Variant<T1, T2, T3>(value);
		public static explicit operator T2(Variant<T1, T2, T3> var) => var.Value as T2;

		public static implicit operator Variant<T1, T2, T3>(T3 value) => new Variant<T1, T2, T3>(value);
		public static explicit operator T3(Variant<T1, T2, T3> var) => var.Value as T3;

		public static implicit operator Variant<T1, T2, T3>(Variant<T1, T2> variant)
			=> new Variant<T1, T2, T3>(variant.Value);

		public override string ToString() => Value?.ToString() ?? "";

		public override int GetHashCode() => Value is null ? 0 : Value.GetHashCode();

		public override bool Equals(object other)
		{
			switch (other)
			{
				case IVariant variant: return Equals(Value, variant.Value);
				default: return Equals(Value, other);
			}
		}

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
			=> new VariantImmutableMetaObject(parameter, this);
	}
}