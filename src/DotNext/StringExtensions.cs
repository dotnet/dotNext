using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using static System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext;

using StringTemplate = Buffers.MemoryTemplate<char>;

/// <summary>
/// Represents various extension methods for type <see cref="string"/>.
/// </summary>
public static class StringExtensions
{
    private static readonly SpanAction<char, string> CopyAndReverse = CreateReversedString;

    private static void CreateReversedString(Span<char> output, string origin)
    {
        origin.AsSpan().CopyTo(output);
        output.Reverse();
    }

    /// <summary>
    /// Returns alternative string if first string argument
    /// is <see langword="null"/> or empty.
    /// </summary>
    /// <example>
    /// This method is equivalent to the following code:
    /// <code>
    /// var result = string.IsNullOrEmpty(str) ? alt : str;
    /// </code>
    /// </example>
    /// <param name="str">A string to check.</param>
    /// <param name="alt">Alternative string to be returned if original string is <see langword="null"/> or empty.</param>
    /// <returns>Original or alternative string.</returns>
    [Obsolete("This method is easily replaceable with pattern matching: expression is { Length: > 0 } str ? str : alt")]
    public static string IfNullOrEmpty(this string? str, string alt)
        => str is { Length: > 0 } ? str : alt;

    /// <summary>
    /// Reverse string characters.
    /// </summary>
    /// <param name="str">The string to reverse.</param>
    /// <returns>The string in inverse order of characters.</returns>
    public static string Reverse(this string str)
    {
        var length = str.Length;
        return length > 0 ? string.Create(length, str, CopyAndReverse) : string.Empty;
    }

    /// <summary>
    /// Trims the source string to specified length if it exceeds it.
    /// If source string is less that <paramref name="maxLength" /> then the source string returned.
    /// </summary>
    /// <param name="str">Source string.</param>
    /// <param name="maxLength">Maximum length.</param>
    /// <returns>Trimmed string value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is less than zero.</exception>
    [return: NotNullIfNotNull("str")]
    public static string? TrimLength(this string? str, int maxLength)
    {
        if (maxLength < 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength));

        if (str is null)
        {
            // return null string
        }
        else if (maxLength is 0)
        {
            str = string.Empty;
        }
        else if (str.Length > maxLength)
        {
            str = new string(str.AsSpan().Slice(0, maxLength));
        }

        return str;
    }

    /// <summary>
    /// Extracts substring from the given string.
    /// </summary>
    /// <remarks>
    /// This method if useful for .NET languages without syntactic support of ranges.
    /// </remarks>
    /// <param name="str">The instance of string.</param>
    /// <param name="range">The range of substring.</param>
    /// <returns>The part of <paramref name="str"/> extracted according with supplied range.</returns>
    public static string Substring(this string str, Range range)
    {
        var (start, length) = range.GetOffsetAndLength(str.Length);
        return str.Substring(start, length);
    }

    /// <summary>
    /// Compiles string template.
    /// </summary>
    /// <param name="template">The string representing template with placeholders.</param>
    /// <param name="placeholder">The placeholder in the template.</param>
    /// <returns>The compiled template that can be used to replace all placeholders with their original values.</returns>
    public static StringTemplate AsTemplate(this string template, string placeholder)
        => new(template.AsMemory(), placeholder);

    /// <summary>
    /// Compiles string template.
    /// </summary>
    /// <param name="template">The string representing template with placeholders.</param>
    /// <param name="placeholder">The placeholder in the template.</param>
    /// <returns>The compiled template that can be used to replace all placeholders with their original values.</returns>
    public static StringTemplate AsTemplate(this string template, char placeholder)
        => new(template.AsMemory(), CreateReadOnlySpan(ref placeholder, 1));

    /// <summary>
    /// Checks whether the growable string is <see langword="null"/> or empty.
    /// </summary>
    /// <param name="builder">The builder to check.</param>
    /// <returns><see langword="true"/>, if builder is <see langword="null"/> or empty.</returns>
    [Obsolete("This method is easily replaceable with pattern matching: sb is not { Length: > 0 };")]
    public static bool IsNullOrEmpty([NotNullWhen(false)] this StringBuilder? builder)
        => builder is not { Length: > 0 };
}