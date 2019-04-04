using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DotNext
{
    using Reflection;

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
            
            internal Tuple(string name)
            {
                Name = name;
                Value = default;
            }

            internal Tuple(E value)
            {
                Value = value;
                Name = default;
            }

            public static implicit operator Tuple(string name) => new Tuple(name);
            public static implicit operator Tuple(E value) => new Tuple(value);
            
            public static implicit operator KeyValuePair<string, E>(Tuple tuple) => new KeyValuePair<string, E>(tuple.Name, tuple.Value);
            public static implicit operator KeyValuePair<E, string>(Tuple tuple) => new KeyValuePair<E, string>(tuple.Value, tuple.Name);

            public bool Equals(Tuple other)
                => Name is null ? other.Name is null && EqualityComparer<E>.Default.Equals(Value, other.Value) : Name == other.Name;

            public override bool Equals(object other) => other is Tuple t && Equals(t);
            public override int GetHashCode() => Name is null ? EqualityComparer<E>.Default.GetHashCode() : Name.GetHashCode();
        }

        private sealed class Mapping : Dictionary<Tuple, Enum<E>>
        {
            internal Mapping(out Enum<E> min, out Enum<E> max)
            {
                var names = Enum.GetNames(typeof(E));
                var values = (E[])Enum.GetValues(typeof(E));
                min = max = default;
                for (var index = 0L; index < names.LongLength; index++)
                {
                    var entry = new Enum<E>(values[index], names[index]);
                    Add(new Tuple(entry.Name), entry);
                    Add(new Tuple(entry.Value), entry);
                    //detect min and max
                    min = entry.Min(min);
                    max = entry.Max(max);
                }
            }
        }

        private static readonly ReadOnlyDictionary<Tuple, Enum<E>> mapping;

        /// <summary>
        /// Maximum enum value.
        /// </summary>
        public static readonly Enum<E> MaxValue;
        
        /// <summary>
        /// Minimum enum value.
        /// </summary>
        public static readonly Enum<E> MinValue;

        static Enum()
        {
            mapping = new ReadOnlyDictionary<Tuple, Enum<E>>(new Mapping(out MinValue, out MaxValue));
        }

        public static bool IsDefined(E value) => mapping.ContainsKey(value);

        public static bool IsDefined(string name) => mapping.ContainsKey(name);

        /// <summary>
        /// Gets enum member by its value.
        /// </summary>
        /// <param name="value">The enum value.</param>
        /// <returns>The enum member.</returns>
        public static Enum<E> GetMember(E value) => mapping[value];

        public static bool TryGetMember(E value, out Enum<E> member) => mapping.TryGetValue(value, out member);

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

        public static bool TryGetMember(int value, out Enum<E> member) => TryGetMember(Conversion<int, E>.Converter(value), out member);
        public static bool TryGetMember(long value, out Enum<E> member) => TryGetMember(Conversion<long, E>.Converter(value), out member);
        public static bool TryGetMember(short value, out Enum<E> member) => TryGetMember(Conversion<short, E>.Converter(value), out member);
        public static bool TryGetMember(byte value, out Enum<E> member) => TryGetMember(Conversion<byte, E>.Converter(value), out member);
        [CLSCompliant(false)]
        public static bool TryGetMember(sbyte value, out Enum<E> member) => TryGetMember(Conversion<sbyte, E>.Converter(value), out member);
        [CLSCompliant(false)]
        public static bool TryGetMember(ushort value, out Enum<E> member) => TryGetMember(Conversion<ushort, E>.Converter(value), out member);
        [CLSCompliant(false)]
        public static bool TryGetMember(uint value, out Enum<E> member) => TryGetMember(Conversion<uint, E>.Converter(value), out member);
        [CLSCompliant(false)]
        public static bool TryGetMember(ulong value, out Enum<E> member) => TryGetMember(Conversion<ulong, E>.Converter(value), out member);

        /// <summary>
        /// Gets enum member by its name.
        /// </summary>
        /// <param name="name">The name of the enum value.</param>
        /// <returns>The enum member.</returns>
        public static Enum<E> GetMember(string name) => mapping[name];

        /// <summary>
        /// Gets declared enum members.
        /// </summary>
        public static IReadOnlyCollection<Enum<E>> Members => mapping.Values;

        /// <summary>
        /// Gets the underlying type of the specified enumeration.
        /// </summary>
        public static Type UnderlyingType => Enum.GetUnderlyingType(typeof(E));

        /// <summary>
        /// Gets code of the underlying primitive type.
        /// </summary>
        public static TypeCode UnderlyingTypeCode => UnderlyingType.GetTypeCode();

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
        public string Name => name ?? Value.ToString();

        /// <summary>
        /// Converts typed enum wrapper into actual enum value.
        /// </summary>
        /// <param name="en">Enum wrapper to convert.</param>
        public static implicit operator E(Enum<E> en) => en.Value;

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
        public bool Equals(E other) => EqualityComparer<E>.Default.Equals(Value, other);

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
        /// Gets hash code of the enum value.
        /// </summary>
        /// <returns>The hash code of the enum value.</returns>
        public override int GetHashCode() => EqualityComparer<E>.Default.GetHashCode(Value);

        /// <summary>
        /// Returns textual representation of the enum value.
        /// </summary>
        /// <returns>The textual representation of the enum value.</returns>
        public override string ToString() => Value.ToString();

        string IFormattable.ToString(string format, IFormatProvider provider) => Value.ToString();

        public static explicit operator long(Enum<E> member) => ValueTypeExtensions.ToInt64(member.Value);

        public static explicit operator int(Enum<E> member) => ValueTypeExtensions.ToInt32(member.Value);

        public static explicit operator byte(Enum<E> member) => ValueTypeExtensions.ToByte(member.Value);

        public static explicit operator short(Enum<E> member) => ValueTypeExtensions.ToInt16(member.Value);

        [CLSCompliant(false)]
        public static explicit operator sbyte(Enum<E> member) => ValueTypeExtensions.ToSByte(member.Value);

        [CLSCompliant(false)]
        public static explicit operator ulong(Enum<E> member) => ValueTypeExtensions.ToUInt64(member.Value);

        [CLSCompliant(false)]
        public static explicit operator uint(Enum<E> member) => ValueTypeExtensions.ToUInt32(member.Value);

        public static explicit operator ushort(Enum<E> member) => ValueTypeExtensions.ToUInt16(member.Value);
    }
}