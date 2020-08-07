using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Serialization;

namespace DotNext
{
    /// <summary>
    /// Provides strongly typed way to reflect enum type.
    /// </summary>
    /// <typeparam name="TEnum">Enum type to reflect.</typeparam>
    /// <seealso href="https://github.com/dotnet/corefx/issues/34077">EnumMember API</seealso>
    [SuppressMessage("Design", "CA1036")]
    [Serializable]
    public readonly struct Enum<TEnum> : IEquatable<TEnum>, IComparable<TEnum>, IFormattable, IEquatable<Enum<TEnum>>, ISerializable, IConvertible<TEnum>, ICustomAttributeProvider
        where TEnum : struct, Enum
    {
        private readonly struct Tuple : IEquatable<Tuple>
        {
            internal readonly string? Name;
            internal readonly TEnum Value;

            private Tuple(string name)
            {
                Name = name;
                Value = default;
            }

            private Tuple(TEnum value)
            {
                Value = value;
                Name = default;
            }

            public static implicit operator Tuple(string name) => new Tuple(name);

            public static implicit operator Tuple(TEnum value) => new Tuple(value);

            public bool Equals(Tuple other)
                => Name is null ? other.Name is null && EqualityComparer<TEnum>.Default.Equals(Value, other.Value) : Name == other.Name;

            public override bool Equals(object? other) => other is Tuple t && Equals(t);

            public override int GetHashCode() => Name is null ? Value.GetHashCode() : Name.GetHashCode(StringComparison.Ordinal);
        }

        private sealed class Mapping : Dictionary<Tuple, long>
        {
            internal readonly Enum<TEnum>[] Members;

            internal Mapping(out Enum<TEnum> min, out Enum<TEnum> max)
            {
                var names = Enum.GetNames(typeof(TEnum));
                var values = (TEnum[])Enum.GetValues(typeof(TEnum));
                Members = new Enum<TEnum>[names.LongLength];
                min = max = default;
                for (var index = 0L; index < names.LongLength; index++)
                {
                    var entry = Members[index] = new Enum<TEnum>(values[index], names[index]);
                    Add(entry.Name, index);
                    this[entry.Value] = index;

                    // detect min and max
                    min = entry.Value.CompareTo(min.Value) < 0 ? entry : min;
                    max = entry.Value.CompareTo(max.Value) > 0 ? entry : max;
                }
            }

            internal Enum<TEnum> this[string name] => Members[base[name]];

            internal bool TryGetValue(TEnum value, out Enum<TEnum> member)
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

        private static readonly Mapping EnumMapping = new Mapping(out MinValue, out MaxValue);

        /// <summary>
        /// Maximum enum value.
        /// </summary>
        public static readonly Enum<TEnum> MaxValue;

        /// <summary>
        /// Minimum enum value.
        /// </summary>
        public static readonly Enum<TEnum> MinValue;

        /// <summary>
        /// Returns an indication whether a constant with a specified value exists in a enumeration of type <typeparamref name="TEnum"/>.
        /// </summary>
        /// <param name="value">The value of a constant in <typeparamref name="TEnum"/>.</param>
        /// <returns><see langword="true"/> if a constant in <typeparamref name="TEnum"/> has a value equal to <paramref name="value"/>; otherwise, <see langword="false"/>.</returns>
        public static bool IsDefined(TEnum value) => EnumMapping.ContainsKey(value);

        /// <summary>
        /// Returns an indication whether a constant with a specified name exists in a enumeration of type <typeparamref name="TEnum"/>.
        /// </summary>
        /// <param name="name">The name of a constant in <typeparamref name="TEnum"/>.</param>
        /// <returns><see langword="true"/> if a constant in <typeparamref name="TEnum"/> has a name equal to <paramref name="name"/>; otherwise, <see langword="false"/>.</returns>
        public static bool IsDefined(string name) => EnumMapping.ContainsKey(name);

        /// <summary>
        /// Gets enum member by its value.
        /// </summary>
        /// <param name="value">The enum value.</param>
        /// <returns>The enum member.</returns>
        public static Enum<TEnum> GetMember(TEnum value) => EnumMapping.TryGetValue(value, out var result) ? result : new Enum<TEnum>(value, null);

        /// <summary>
        /// Attempts to retrieve enum member which constant value is equal to the given value.
        /// </summary>
        /// <param name="value">Enum value.</param>
        /// <param name="member">Enum member which constant value is equal to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/>, if there are member declared the given constant value exist; otherwise, <see langword="false"/>.</returns>
        public static bool TryGetMember(TEnum value, out Enum<TEnum> member) => EnumMapping.TryGetValue(value, out member);

        /// <summary>
        /// Attempts to retrieve enum member which name is equal to the given value.
        /// </summary>
        /// <param name="name">The name of a constant.</param>
        /// <param name="member">Enum member which name is equal to <paramref name="name"/>.</param>
        /// <returns><see langword="true"/>, if there are member declared the given constant value exist; otherwise, <see langword="false"/>.</returns>
        public static bool TryGetMember(string name, out Enum<TEnum> member)
        {
            if (Enum.TryParse<TEnum>(name, out var value))
            {
                member = new Enum<TEnum>(value, name);
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
        public static Enum<TEnum> GetMember(string name) => EnumMapping[name];

        /// <summary>
        /// Gets declared enum members.
        /// </summary>
        public static IReadOnlyList<Enum<TEnum>> Members => EnumMapping.Members;

        /// <summary>
        /// Gets the underlying type of the specified enumeration.
        /// </summary>
        public static Type UnderlyingType => Enum.GetUnderlyingType(typeof(TEnum));

        /// <summary>
        /// Gets code of the underlying primitive type.
        /// </summary>
        public static TypeCode UnderlyingTypeCode => Type.GetTypeCode(typeof(TEnum));

        private const string NameSerData = "Name";
        private const string ValueSerData = "Value";
        private readonly string? name;

        private Enum(TEnum value, string? name)
        {
            Value = value;
            this.name = name;
        }

        [SuppressMessage("Usage", "CA1801", Justification = "context is required by .NET serialization framework")]
        private Enum(SerializationInfo info, StreamingContext context)
        {
            name = info.GetString(NameSerData);
            Value = (TEnum)info.GetValue(ValueSerData, typeof(TEnum));
        }

        private FieldInfo? Field
            => typeof(TEnum).GetField(Name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        /// <inheritdoc />
        object[] ICustomAttributeProvider.GetCustomAttributes(bool inherit)
            => Field?.GetCustomAttributes(inherit) ?? Array.Empty<object>();

        /// <inheritdoc />
        object[] ICustomAttributeProvider.GetCustomAttributes(Type attributeType, bool inherit)
            => Field?.GetCustomAttributes(attributeType, inherit) ?? Array.Empty<object>();

        /// <summary>
        /// Retrieves a collection of custom attributes of a specified type
        /// that are applied to this enum member.
        /// </summary>
        /// <typeparam name="T">The type of attribute to search for.</typeparam>
        /// <returns>
        /// A collection of the custom attributes that are applied to element and that match
        /// <typeparamref name="T"/>, or an empty collection if no such attributes exist.
        /// </returns>
        public IEnumerable<T> GetCustomAttributes<T>()
            where T : Attribute
            => Field?.GetCustomAttributes<T>(false) ?? Array.Empty<T>();

        /// <summary>
        /// Retrieves a custom attribute of a specified type that is applied to this enum member.
        /// </summary>
        /// <typeparam name="T">The type of attribute to search for.</typeparam>
        /// <returns>A custom attribute that matches <typeparamref name="T"/>, or <see langword="null"/> if no such attribute is found.</returns>
        public T? GetCustomAttribute<T>()
            where T : Attribute
            => Field?.GetCustomAttribute<T>(false) ?? null;

        /// <summary>
        /// Indicates whether the one or more attributes of the specified type
        /// or of its derived types is applied to this enum value.
        /// </summary>
        /// <param name="attributeType">The type of custom attribute to search for. The search includes derived types.</param>
        /// <param name="inherit">
        /// <see langword="true"/> to search this member's inheritance chain to find the attributes; otherwise, <see langword="false"/>.</param>
        /// <returns><see langword="true"/> if one or more instances of attributeType or any of its derived types is applied to this member; otherwise, <see langword="false"/>.</returns>
        public bool IsDefined(Type attributeType, bool inherit = false)
            => Field?.IsDefined(attributeType, inherit) ?? false;

        /// <summary>
        /// Determines whether one or more bit fields associated
        /// with the current instance are set in the given value.
        /// </summary>
        /// <param name="flags">An enumeration value.</param>
        /// <returns><see langword="true"/>, if <see cref="Value"/> bits are set in <paramref name="flags"/>.</returns>
        public bool IsFlag(TEnum flags) => Runtime.Intrinsics.HasFlag(flags, Value);

        /// <summary>
        /// Represents value of the enum member.
        /// </summary>
        public TEnum Value { get; }

        /// <inheritdoc/>
        TEnum IConvertible<TEnum>.Convert() => Value;

        /// <summary>
        /// Represents name of the enum member.
        /// </summary>
        public string Name => name ?? ValueTypeExtensions.ToString(Value);

        /// <summary>
        /// Converts typed enum wrapper into actual enum value.
        /// </summary>
        /// <param name="en">Enum wrapper to convert.</param>
        public static implicit operator TEnum(in Enum<TEnum> en) => en.Value;

        /// <summary>
        /// Compares this enum value with other.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns>Comparison result.</returns>
        public int CompareTo(TEnum other) => Comparer<TEnum>.Default.Compare(Value, other);

        /// <summary>
        /// Determines whether this value equals to the other enum value.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns>Equality check result.</returns>
        public bool Equals(TEnum other) => EqualityComparer<TEnum>.Default.Equals(Value, other);

        /// <summary>
        /// Determines whether two enum members are equal.
        /// </summary>
        /// <param name="other">Other member to compare.</param>
        /// <returns><see langword="true"/> if this enum member is the same as other; otherwise, <see langword="false"/>.</returns>
        public bool Equals(Enum<TEnum> other) => Equals(other.Value) && Equals(Name, other.Name);

        /// <summary>
        /// Determines whether this value equals to the other enum value.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns>Equality check result.</returns>
        public override bool Equals(object? other) => other switch
        {
            Enum<TEnum> en => Equals(en),
            TEnum en => Equals(en),
            _ => false,
        };

        /// <summary>
        /// Gets hash code of the enum member.
        /// </summary>
        /// <returns>The hash code of the enum member.</returns>
        public override int GetHashCode() => HashCode.Combine(Value, Name);

        /// <summary>
        /// Returns textual representation of the enum value.
        /// </summary>
        /// <returns>The textual representation of the enum value.</returns>
        public override string ToString() => ValueTypeExtensions.ToString(Value);

        /// <inheritdoc/>
        string IFormattable.ToString(string format, IFormatProvider provider) => ValueTypeExtensions.ToString(Value, format, provider);

        /// <summary>
        /// Determines whether two enum members are equal.
        /// </summary>
        /// <param name="first">The first member to compare.</param>
        /// <param name="second">The second member to compare.</param>
        /// <returns><see langword="true"/> if this enum member is the same as other; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(Enum<TEnum> first, Enum<TEnum> second) => first.Equals(second);

        /// <summary>
        /// Determines whether two enum members are not equal.
        /// </summary>
        /// <param name="first">The first member to compare.</param>
        /// <param name="second">The second member to compare.</param>
        /// <returns><see langword="true"/> if this enum member is not the same as other; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(Enum<TEnum> first, Enum<TEnum> second) => !first.Equals(second);

        /// <inheritdoc/>
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(NameSerData, name, typeof(string));
            info.AddValue(ValueSerData, Value);
        }
    }
}