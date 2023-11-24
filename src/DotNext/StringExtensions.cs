using System.Diagnostics.CodeAnalysis;
using static System.Runtime.InteropServices.MemoryMarshal;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext;

/// <summary>
/// Represents various extension methods for type <see cref="string"/>.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Reverse string characters.
    /// </summary>
    /// <param name="str">The string to reverse.</param>
    /// <returns>The string in inverse order of characters.</returns>
    [return: NotNullIfNotNull(nameof(str))]
    public static string? Reverse(this string? str)
    {
        if (str is { Length: > 0 })
        {
            str = new(str);
            CreateSpan(ref Unsafe.AsRef(in str.GetPinnableReference()), str.Length).Reverse();
        }

        return str;
    }

    /// <summary>
    /// Trims the source string to specified length if it exceeds it.
    /// If source string is less that <paramref name="maxLength" /> then the source string returned.
    /// </summary>
    /// <param name="str">Source string.</param>
    /// <param name="maxLength">Maximum length.</param>
    /// <returns>Trimmed string value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is less than zero.</exception>
    [return: NotNullIfNotNull(nameof(str))]
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
}