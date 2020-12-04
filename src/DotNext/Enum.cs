using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
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
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Enum<TEnum> : IEquatable<TEnum>, IComparable<TEnum>, IFormattable, IEquatable<Enum<TEnum>>, ISerializable, IConvertible<TEnum>, ICustomAttributeProvider
        where TEnum : struct, Enum
    {
        private sealed class Mapping : Dictionary<TEnum, string>
        {
            internal Mapping(out TEnum min, out TEnum max, out Array values)
            {
                min = max = default;
                var enumType = typeof(TEnum);
                values = Enum.GetValues(enumType);
                for (var i = 0L; i < values.LongLength; i++)
                {
                    var boxedValue = values.GetValue(i);
                    var value = (TEnum)boxedValue;
                    this[value] = Enum.GetName(enumType, boxedValue);

                    // detect min and max
                    min = value.CompareTo(min) < 0 ? value : min;
                    max = value.CompareTo(max) > 0 ? value : max;
                }
            }
        }

        private static readonly IReadOnlyDictionary<TEnum, string> EnumMapping;

        static Enum()
        {
            var mapping = new Mapping(out var min, out var max, out var values);
            mapping.TrimExcess();
            EnumMapping = mapping;

            MaxValue = new Enum<TEnum>(max);
            MinValue = new Enum<TEnum>(min);
            Members = Array.ConvertAll((TEnum[])values, GetMember);
        }

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

        private static FieldInfo? GetField(string name)
            => typeof(TEnum).GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        /// <summary>
        /// Returns an indication whether a constant with a specified name exists in a enumeration of type <typeparamref name="TEnum"/>.
        /// </summary>
        /// <param name="name">The name of a constant in <typeparamref name="TEnum"/>.</param>
        /// <returns><see langword="true"/> if a constant in <typeparamref name="TEnum"/> has a name equal to <paramref name="name"/>; otherwise, <see langword="false"/>.</returns>
        public static bool IsDefined(string name) => !(GetField(name) is null);

        /// <summary>
        /// Gets enum member by its value.
        /// </summary>
        /// <param name="value">The enum value.</param>
        /// <returns>The enum member.</returns>
        public static Enum<TEnum> GetMember(TEnum value) => new Enum<TEnum>(value);

        /// <summary>
        /// Attempts to retrieve enum member which constant value is equal to the given value.
        /// </summary>
        /// <param name="value">Enum value.</param>
        /// <param name="member">Enum member which constant value is equal to <paramref name="value"/>.</param>
        /// <returns><see langword="true"/>, if there are member declared the given constant value exist; otherwise, <see langword="false"/>.</returns>
        public static bool TryGetMember(TEnum value, out Enum<TEnum> member)
        {
            if (EnumMapping.ContainsKey(value))
            {
                member = new Enum<TEnum>(value);
                return true;
            }

            member = default;
            return false;
        }

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
                member = new Enum<TEnum>(value);
                return true;
            }

            member = default;
            return false;
        }

        /// <summary>
        /// Gets enum member by its case-sensitive name.
        /// </summary>
        /// <param name="name">The name of the enum value.</param>
        /// <returns>The enum member.</returns>
        /// <exception cref="KeyNotFoundException">Enum member with the requested name doesn't exist in enum.</exception>
        public static Enum<TEnum> GetMember(string name)
            => Enum.TryParse<TEnum>(name, out var value) ? new Enum<TEnum>(value) : throw new KeyNotFoundException();

        /// <summary>
        /// Gets declared enum members.
        /// </summary>
        public static IReadOnlyList<Enum<TEnum>> Members { get; }

        /// <summary>
        /// Gets the underlying type of the specified enumeration.
        /// </summary>
        public static Type UnderlyingType => Enum.GetUnderlyingType(typeof(TEnum));

        /// <summary>
        /// Gets code of the underlying primitive type.
        /// </summary>
        public static TypeCode UnderlyingTypeCode => Type.GetTypeCode(typeof(TEnum));

        private const string ValueSerData = "Value";

        private Enum(TEnum value) => Value = value;

        private Enum(SerializationInfo info, StreamingContext context)
            => Value = (TEnum)info.GetValue(ValueSerData, typeof(TEnum));

        private FieldInfo? Field
            => EnumMapping.TryGetValue(Value, out var name) ? GetField(name) : null;

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
        public string Name => EnumMapping.TryGetValue(Value, out var name) ? name : ValueTypeExtensions.ToString(Value);

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
        public bool Equals(Enum<TEnum> other) => Equals(other.Value);

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
        public override int GetHashCode() => Value.GetHashCode();

        /// <summary>
        /// Returns textual representation of the enum value.
        /// </summary>
        /// <returns>The textual representation of the enum value.</returns>
        public override string ToString() => Name;

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
            => info.AddValue(ValueSerData, Value);
    }
}