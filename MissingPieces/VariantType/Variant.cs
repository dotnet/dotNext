using System;

namespace MissingPieces.VariantType
{

    /// <summary>
    /// Represents value that can be one of two possible types.
    /// </summary>
    /// 
    public readonly struct Variant<T1, T2>: IEquatable<Variant<T1, T2>>, IVariant
        where T1: class
        where T2: class
    {
        private readonly object value;
        
        public bool IsNull => value is null;

        private Variant(object value) => this.value = value;

        public Variant(T1 value)
            : this(value.Upcast<object, T1>())
        {
        }

        public Variant(T2 value)
            : this(value.Upcast<object, T2>())
        {
        }

        object IVariant.Value => value;

        public Optional<T1> First => (value as T1).EmptyIfNull();

        public Optional<T2> Second => (value as T2).EmptyIfNull();

        public Optional<R> Map<R>(Func<T1, R> mapper1, Func<T2, R> mapper2)
        {
            switch(value)
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
            switch(value)
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
        public Variant<T2, T1> Permute() => new Variant<T2, T1>(value);

        public T2 UnifyFirst(Func<T1, T2> mapper) => value is T1 first ? mapper(first) : value as T2;

        public T1 UnifySecond(Func<T2, T1> mapper) => value is T2 second ? mapper(second) : value as T1;

        public static implicit operator Variant<T1, T2>(T1 value) => new Variant<T1, T2>(value);
        
        public static explicit operator T1(Variant<T1, T2> var) => var.value as T1;

        public static implicit operator Variant<T1, T2>(T2 value) => new Variant<T1, T2>(value);
        
        public static explicit operator T2(Variant<T1, T2> var) => var.value as T2;

        public bool Equals(Variant<T1, T2> other) => Equals(value, other.value);

        public bool Equals(T1 other) => Equals(value, other);

        public bool Equals(T2 other) => Equals(value, other);

        public static bool operator==(Variant<T1, T2> first, T1 second) => first.Equals(second);
        public static bool operator!=(Variant<T1, T2> first, T1 second) => !first.Equals(second);

        public static bool operator==(Variant<T1, T2> first, T2 second) => first.Equals(second);
        public static bool operator!=(Variant<T1, T2> first, T2 second) => !first.Equals(second);

        public static bool operator==(Variant<T1, T2> first, Variant<T1, T2> second) => first.Equals(second);
        public static bool operator!=(Variant<T1, T2> first, Variant<T1, T2> second) => !first.Equals(second);

        public override string ToString() => value?.ToString() ?? "";

        public override int GetHashCode() => value is null ? 0: value.GetHashCode();

        public override bool Equals(object other)
        {
            switch(other)
            {
                case T1 first: return Equals(first);
                case T2 second: return Equals(second);
                case Variant<T1, T2> variant: return Equals(variant);
                case IVariant variant: return Equals(value, variant.Value);
                default: return false;
            }
        }
    }
}