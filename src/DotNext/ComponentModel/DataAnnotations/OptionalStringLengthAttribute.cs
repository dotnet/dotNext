using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace DotNext.ComponentModel.DataAnnotations;

/// <summary>
/// Specifies the minimum and maximum length of characters that are allowed in a data field
/// of type <see cref="Optional{T}"/>.
/// </summary>
public sealed class OptionalStringLengthAttribute : StringLengthAttribute
{
    /// <summary>
    /// Initializes a new attribute.
    /// </summary>
    /// <param name="maximumLength">The maximum length of a string.</param>
    public OptionalStringLengthAttribute(int maximumLength)
        : base(maximumLength)
    {
    }

    /// <inheritdoc/>
    public override bool IsValid(object? value)
        => base.IsValid(value is Optional<string> ? Unsafe.Unbox<Optional<string>>(value).ValueOrDefault : value);
}