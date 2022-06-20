using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Runtime.CompilerServices;

/// <summary>
/// Represents container for value type.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
[Obsolete("Use BoxedValue<T> data type instead")]
public sealed class Shared<T>
    where T : struct
{
    /// <summary>
    /// Represents a value in the container.
    /// </summary>
    public T Value;

    /// <summary>
    /// Boxes nullable value type.
    /// </summary>
    /// <param name="value">The value to be placed to the container.</param>
    /// <returns>The boxed representation of the value; or <see langword="null"/> if <paramref name="value"/> is <see langword="null"/>.</returns>
    [return: NotNullIfNotNull("value")]
    public static implicit operator Shared<T>?(in T? value)
        => value.HasValue ? new() { Value = value.GetValueOrDefault() } : null;

    /// <summary>
    /// Places the value to the container.
    /// </summary>
    /// <param name="value">The value to be placed to the container.</param>
    /// <returns>The boxed representation of the value.</returns>
    public static implicit operator Shared<T>(T value) => new() { Value = value };

    /// <summary>
    /// Converts the value in this container to string.
    /// </summary>
    /// <returns><see cref="Value"/> converted to the string.</returns>
    public override string? ToString() => Value.ToString();
}