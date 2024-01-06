using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace DotNext.ComponentModel.DataAnnotations;

/// <summary>
/// Specifies the minimum and maximum length of characters that are allowed in a data field
/// of type <see cref="Optional{T}"/>.
/// </summary>
/// <remarks>
/// Initializes a new attribute.
/// </remarks>
/// <param name="maximumLength">The maximum length of a string.</param>
public sealed class OptionalStringLengthAttribute(int maximumLength) : StringLengthAttribute(maximumLength)
{
    /// <inheritdoc/>
    public override bool IsValid(object? value)
        => base.IsValid(value is Optional<string> ? Unsafe.Unbox<Optional<string>>(value).ValueOrDefault : value);
}