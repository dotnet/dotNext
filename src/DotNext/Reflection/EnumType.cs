using System.Reflection;

namespace DotNext.Reflection;

/// <summary>
/// Provides reflection helpers for enum types.
/// </summary>
public static class EnumType
{
    private static FieldInfo? GetField<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        const BindingFlags publicStaticField = BindingFlags.Public | BindingFlags.Static;
        var fieldName = Enum.GetName<TEnum>(value);
        return string.IsNullOrEmpty(fieldName) ? null : typeof(TEnum).GetField(fieldName, publicStaticField);
    }

    /// <summary>
    /// Gets custom attribute associated with the specified enum value.
    /// </summary>
    /// <param name="value">The value to be reflected.</param>
    /// <typeparam name="TEnum">The type of the enum to be reflected.</typeparam>
    /// <typeparam name="TAttribute">The type of attribute to search for.</typeparam>
    /// <returns>A custom attribute that matches <typeparamref name="TAttribute"/>, or <see langword="null"/> if no such attribute is found.</returns>
    public static TAttribute? GetCustomAttribute<TEnum, TAttribute>(this TEnum value)
        where TEnum : struct, Enum
        where TAttribute : Attribute
        => GetField(value)?.GetCustomAttribute<TAttribute>(false);

    /// <summary>
    /// Gets custom attributes associayed with the specified enum value.
    /// </summary>
    /// <param name="value">The value to be reflected.</param>
    /// <typeparam name="TEnum">The type of the enum to be reflected.</typeparam>
    /// <typeparam name="TAttribute">The type of attribute to search for.</typeparam>
    /// <returns>A collection of the custom attributes associated with <paramref name="value"/>.</returns>
    public static IEnumerable<TAttribute> GetCustomAttributes<TEnum, TAttribute>(this TEnum value)
        where TEnum : struct, Enum
        where TAttribute : Attribute
        => GetField(value)?.GetCustomAttributes<TAttribute>(false) ?? Array.Empty<TAttribute>();
}