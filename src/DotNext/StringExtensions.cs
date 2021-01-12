using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using static System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext
{
    using CharSequence = Buffers.ChunkSequence<char>;
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
        public static string IfNullOrEmpty(this string? str, string alt)
            => string.IsNullOrEmpty(str) ? alt : str;

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
        /// Compares two string using <see cref="StringComparison.OrdinalIgnoreCase" />.
        /// </summary>
        /// <param name="strA">String A. Can be <see langword="null"/>.</param>
        /// <param name="strB">String B. Can be <see langword="null"/>.</param>
        /// <returns><see langword="true"/>, if the first string is equal to the second string; otherwise, <see langword="false"/>.</returns>
        [Obsolete("Use string.Equals(string, string, StringComparison) static method instead")]
        public static bool IsEqualIgnoreCase(this string? strA, string? strB)
            => string.Equals(strA, strB, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Trims the source string to specified length if it exceeds it.
        /// If source string is less that <paramref name="maxLength" /> then the source string returned.
        /// </summary>
        /// <param name="str">Source string.</param>
        /// <param name="maxLength">Maximum length.</param>
        /// <returns>Trimmed string value.</returns>
        [return: NotNullIfNotNull("str")]
        public static string? TrimLength(this string? str, int maxLength)
            => str is null || str.Length <= maxLength ? str : str.Substring(0, maxLength);

        /// <summary>
        /// Split a string into several substrings, each has a length not greater the specified one.
        /// </summary>
        /// <param name="str">The string to split.</param>
        /// <param name="chunkSize">The maximum length of the substring in the sequence.</param>
        /// <returns>The sequence of substrings.</returns>
        [Obsolete("Use ReadOnlyMemory<T>.Slice instead", true)]
        public static CharSequence Split(string str, int chunkSize) => new CharSequence(str.AsMemory(), chunkSize);

        /// <summary>
        /// Gets managed pointer to the first character in the string.
        /// </summary>
        /// <param name="str">The string data.</param>
        /// <returns>The managed pointer to the first character in the string.</returns>
        [Obsolete("Use String.GetPinnableReference method instead")]
        public static ref readonly char GetRawData(string str)
            => ref GetReference(str.AsSpan());

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
            => new StringTemplate(template.AsMemory(), placeholder);

        /// <summary>
        /// Compiles string template.
        /// </summary>
        /// <param name="template">The string representing template with placeholders.</param>
        /// <param name="placeholder">The placeholder in the template.</param>
        /// <returns>The compiled template that can be used to replace all placeholders with their original values.</returns>
        public static StringTemplate AsTemplate(this string template, char placeholder)
            => new StringTemplate(template.AsMemory(), CreateReadOnlySpan(ref placeholder, 1));
    }
}