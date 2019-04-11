using System;
using System.Collections.Generic;

namespace DotNext
{
    /// <summary>
    /// Provides strongly typed way to reflect enum type.
    /// </summary>
    /// <typeparam name="E">Enum type to reflect.</typeparam>
    /// <seealso href="https://github.com/dotnet/corefx/issues/34077">EnumMember API</seealso>
    public readonly struct Enum<E>: IEquatable<E>, IComparable<E>, IFormattable, IComparable<Enum<E>>
        where E : struct, Enum
    {
        private readonly struct Tuple: IEquatable<Tuple>
        {
            internal readonly string Name;
            internal readonly E Value;
            
            private Tuple(string name)
            {
                Name = name;
                Value = default;
            }

            private Tuple(E value)
            {
                Value = value;
                Name = default;
            }

            public static implicit operator Tuple(string name) => new Tuple(name);
            public static implicit operator Tuple(E value) => new Tuple(value);

            public bool Equals(Tuple other)
                => Name is null ? other.Name is null && ValueTypeExtensions.ToUInt64(Value) == ValueTypeExtensions.ToUInt64(other.Value): Name == other.Name;

            public override bool Equals(object other) => other is Tuple t && Equals(t);
            public override int GetHashCode() => Name is null ? Value.GetHashCode() : Name.GetHashCode();
        }

        private sealed class Mapping : Dictionary<Tuple, long>
        {
            internal readonly Enum<E>[] Members;

            internal Mapping(out Enum<E> min, out Enum<E> max)
            {
                var names = Enum.GetNames(typeof(E));
                var values = (E[])Enum.GetValues(typeof(E));
                Members = new Enum<E>[names.LongLength];
                min = max = default;
                for (var index = 0L; index < names.LongLength; index++)
                {
                    var entry = Members[index] = new Enum<E>(values[index], names[index]);
                    Add(entry.Name, index);
                    base[entry.Value] = index;
                    //detect min and max
                    min = entry.Min(min);
                    max = entry.Max(max);
                }
            }

            internal Enum<E> this[string name] => Members[base[name]];
            
            internal bool TryGetValue(E value, out Enum<E> member)
            {
                if (base.TryGetValue(value, out var index))
                {
                    member = Members[index];
                    return true;
                }
                else
                {
                    member = default;
                    return false;
                }
            }
        }

        private static readonly Mapping mapping = new Mapping(out MinValue, out MaxValue);

        /// <summary>
        /// Maximum enum value.
        /// </summary>
        public static readonly Enum<E> MaxValue;
        
        /// <summary>
        /// Minimum enum value.
        /// </summary>
        public static readonly Enum<E> MinValue;

        /// <summary>
        /// Returns an indication whether a constant with a specified value exists in a enumeration of type <typeparamref name="E"/>.
        /// </summary>
        /// <param name="value">The value of a constant in <typeparamref name="E"/>.</param>
        /// <returns><see langword="true"/> if a constant in <typeparamref name="E"/> has a value equal to <paramref name="value"/>; otherwise, <see langword="false"/>.</returns>
        public static bool IsDefined(E value) => mapping.ContainsKey(value);

        /// <summary>
        /// Returns an indication whether a constant with a specified name exists in a enumeration of type <typeparamref name="E"/>.
        /// </summary>
        /// <param name="name">The name of a constant in <typeparamref name="E"/>.</param>
        /// <returns><see langword="true"/> if a constant in <typeparamref name="E"/> has a name equal to <paramref name="name"/>; otherwise, <see langword="false"/>.</returns>
        public static bool IsDefined(string name) => mapping.ContainsKey(name);

        /// <summary>
        /// Gets enum member by its value.
        /// </summary>
        /// <param name="value">The enum value.</param>
        /// <returns>The enum member.</returns>
        public static Enum<E> GetMember(E value) => mapping.TryGetValue(value, out var result) ? result : new Enum<E>(value, null);

        /// <summary>
        /// Attempts to retrieve enum member which constant value is equal to the given value.
        /// </summary>
        /// <param name="value">Enum value.</param>
        /// <param name="member">Enum member which constant value is equal to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/>, if there are member declared the given constant value exist; otherwise, <see langword="false"/>.</returns>
        public static bool TryGetMember(E value, out Enum<E> member) => mapping.TryGetValue(value, out member);

        /// <summary>
        /// Attempts to retrieve enum member which name is equal to the given value.
        /// </summary>
        /// <param name="name">The name of a constant.</param>
        /// <param name="member">Enum member which name is equal to <paramref name="name"/>.</param>
        /// <returns><see langword="true"/>, if there are member declared the given constant value exist; otherwise, <see langword="false"/>.</returns>
        public static bool TryGetMember(string name, out Enum<E> member)
        {
            if(Enum.TryParse<E>(name, out var value))
            {
                member = new Enum<E>(value, name);
                return true;
            }
            else
            {
                member = default;
                return false;
            }
        }

        /// <summary>
        /// Gets enum member by its case-sensitive name.
        /// </summary>
        /// <param name="name">The name of the enum value.</param>
        /// <returns>The enum member.</returns>
        /// <exception cref="KeyNotFoundException">Enum member with the requested name doesn't exist in enum.</exception>
        public static Enum<E> GetMember(string name) => mapping[name];

        /// <summary>
        /// Gets declared enum members.
        /// </summary>
        public static IReadOnlyList<Enum<E>> Members => mapping.Members;

        /// <summary>
        /// Gets the underlying type of the specified enumeration.
        /// </summary>
        public static Type UnderlyingType => Enum.GetUnderlyingType(typeof(E));

        /// <summary>
        /// Gets code of the underlying primitive type.
        /// </summary>
        public static TypeCode UnderlyingTypeCode => ValueTypeExtensions.GetTypeCode<E>();

        private readonly string name;

        private Enum(E value, string name)
        {
            Value = value;
            this.name = name;
        }

        /// <summary>
        /// Represents value of the enum member.
        /// </summary>
        public E Value { get; }

        /// <summary>
        /// Represents name of the enum member.
        /// </summary>
        public string Name => name ?? ValueTypeExtensions.ToString(Value);

        /// <summary>
        /// Converts typed enum wrapper into actual enum value.
        /// </summary>
        /// <param name="en">Enum wrapper to convert.</param>
        public static implicit operator E(in Enum<E> en) => en.Value;

        /// <summary>
        /// Compares this enum value with other.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns>Comparison result.</returns>
        public int CompareTo(E other) => Comparer<E>.Default.Compare(Value, other);

        int IComparable<Enum<E>>.CompareTo(Enum<E> other) => CompareTo(other);

        /// <summary>
        /// Determines whether this value equals to the other enum value.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns>Equality check result.</returns>
        public bool Equals(E other) => ValueTypeExtensions.ToUInt64(Value) == ValueTypeExtensions.ToUInt64(other);

        /// <summary>
        /// Determines whether this value equals to the other enum value.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns>Equality check result.</returns>
        public override bool Equals(object other)
        {
            switch(other)
            {
                case Enum<E> en:
                    return Equals(en);
                case E en:
                    return Equals(en);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets hash code of the enum member.
        /// </summary>
        /// <returns>The hash code of the enum member.</returns>
        public override int GetHashCode()
        {
            var hashCode = -1670801664;
            hashCode = hashCode * -1521134295 + Value.GetHashCode();
            hashCode = hashCode * -1521134295 + Name.GetHashCode();
            return hashCode;
        }

        /// <summary>
        /// Returns textual representation of the enum value.
        /// </summary>
        /// <returns>The textual representation of the enum value.</returns>
        public override string ToString() => ValueTypeExtensions.ToString(Value);

        string IFormattable.ToString(string format, IFormatProvider provider) => ValueTypeExtensions.ToString(Value, format, provider);
    }
}