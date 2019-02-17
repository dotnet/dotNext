using System;
using System.Collections.Generic;
using System.Linq;

namespace DotNext
{
    using Reflection;

    /// <summary>
    /// Provides strongly typed way to reflect enum type.
    /// </summary>
    /// <typeparam name="E">Enum type to reflect.</typeparam>
    public readonly struct Enum<E>: IEquatable<E>, IComparable<E>, IFormattable
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

        private abstract class Mapping : Dictionary<Tuple, (string Name, E Value)>
        {
            internal Mapping(out E min, out E max)
            {
                var names = Enum.GetNames(typeof(E));
                var values = (E[])Enum.GetValues(typeof(E));
                min = max = default;
                for (var index = 0L; index < names.LongLength; index++)
                {
                    (string name, E value) entry = (names[index], values[index]);
                    Add(new Tuple(entry.name), entry);
                    Add(new Tuple(entry.value), entry);
                    //detect min and max
                    min = Range.Min(min, entry.value, Comparer<E>.Default.Compare);
                    max = Range.Max(max, entry.value, Comparer<E>.Default.Compare);
                }
            }
        }

        private abstract class NameToValueMapping: Mapping, IReadOnlyDictionary<string, E>
        {
            internal NameToValueMapping(out E min, out E max)
                : base(out min, out max)
            {
            }

            E IReadOnlyDictionary<string, E>.this[string name] => this[name].Value;

            IEnumerable<string> IReadOnlyDictionary<string, E>.Keys => Keys.Select(tuple => tuple.Name);

            IEnumerable<E> IReadOnlyDictionary<string, E>.Values => Values.Select(entry => entry.Value);

            int IReadOnlyCollection<KeyValuePair<string, E>>.Count => Count / 2;

            bool IReadOnlyDictionary<string, E>.ContainsKey(string name) => ContainsKey(name);

            IEnumerator<KeyValuePair<string, E>> IEnumerable<KeyValuePair<string, E>>.GetEnumerator()
                => Keys.Select(Conversion<Tuple, KeyValuePair<string, E>>.Converter.AsFunc()).GetEnumerator();
            
            bool IReadOnlyDictionary<string, E>.TryGetValue(string name, out E value)
            {
                if (TryGetValue(name, out var result))
                {
                    value = result.Value;
                    return true;
                }
                else
                {
                    value = default;
                    return false;
                }
            }
        }

        private sealed class ValueToNameMapping: NameToValueMapping, IReadOnlyDictionary<E, string>
        {
            internal ValueToNameMapping(out E min, out E max)
                : base(out min, out max)
            {
            }

            string IReadOnlyDictionary<E, string>.this[E value] => this[value].Name;

            IEnumerable<E> IReadOnlyDictionary<E, string>.Keys => Keys.Select(tuple => tuple.Value);

            IEnumerable<string> IReadOnlyDictionary<E, string>.Values => Values.Select(entry => entry.Name);

            int IReadOnlyCollection<KeyValuePair<E, string>>.Count => Count / 2;

            bool IReadOnlyDictionary<E, string>.ContainsKey(E value) => ContainsKey(value);

            IEnumerator<KeyValuePair<E, string>> IEnumerable<KeyValuePair<E, string>>.GetEnumerator()
                => Keys.Select(Conversion<Tuple, KeyValuePair<E, string>>.Converter.AsFunc()).GetEnumerator();

            bool IReadOnlyDictionary<E, string>.TryGetValue(E key, out string value)
            {
                if (TryGetValue(key, out var result))
                {
                    value = result.Name;
                    return true;
                }
                else
                {
                    value = default;
                    return false;
                }
            }
        }

        private static readonly ValueToNameMapping mapping;

        /// <summary>
        /// Maximum enum value.
        /// </summary>
        public static readonly E MaxValue;
        
        /// <summary>
        /// Minimum enum value.
        /// </summary>
        public static readonly E MinValue;

        static Enum()
        {
            mapping = new ValueToNameMapping(out MinValue, out MaxValue);
        }

        /// <summary>
        /// Gets the underlying type of the specified enumeration.
        /// </summary>
        public static Type UnderlyingType => Enum.GetUnderlyingType(typeof(E));
        
        /// <summary>
        /// Gets mapping between enum value name and its actual value.
        /// </summary>
        public static IReadOnlyDictionary<string, E> Names => mapping;

        /// <summary>
        /// Gets mapping between enum actual value and its name.
        /// </summary>
        public static IReadOnlyDictionary<E, string> Values => mapping;

        private readonly E value;

        private Enum(E value) => this.value = value;

        /// <summary>
        /// Converts typed enum wrapper into actual enum value.
        /// </summary>
        /// <param name="en">Enum wrapper to convert.</param>
        public static implicit operator E(Enum<E> en) => en.value;

        /// <summary>
        /// Wraps enum value.
        /// </summary>
        /// <param name="value">The value to wrap.</param>
        public static implicit operator Enum<E>(E value) => new Enum<E>(value);

        /// <summary>
        /// Compares this enum value with other.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns>Comparison result.</returns>
        public int CompareTo(E other) => Comparer<E>.Default.Compare(value, other);

        /// <summary>
        /// Determines whether this value equals to the other enum value.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns>Equality check result.</returns>
        public bool Equals(E other) => EqualityComparer<E>.Default.Equals(value, other);

        /// <summary>
        /// Determines whether this value equals to the other enum value.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns>Equality check result.</returns>
        public bool Equals(object other)
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
        public override int GetHashCode() => EqualityComparer<E>.Default.GetHashCode(value);

        /// <summary>
        /// Returns textual representation of the enum value.
        /// </summary>
        /// <returns>The textual representation of the enum value.</returns>
        public override string ToString() => value.ToString();

        string IFormattable.ToString(string format, IFormatProvider provider) => value.ToString();
    }
}