using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using M = InlineIL.MethodRef;
using Var = InlineIL.LocalVar;
using Debug = System.Diagnostics.Debug;

namespace DotNext
{
    using Buffers;

    /// <summary>
    /// Represents various extension methods for type <see cref="string"/>.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Returns alternative string if first string argument 
        /// is <see langword="null"/> or empty.
        /// </summary>
        /// <example>
        /// This method is equivalent to
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
            if (str.Length == 0)
                return str;
            using MemoryRental<char> result = str.Length <= 1024 ? stackalloc char[str.Length] : new MemoryRental<char>(str.Length);
            str.AsSpan().CopyTo(result.Span);
            result.Span.Reverse();
            return new string(result.Span);
        }

        /// <summary>
        /// Compares two string using <see cref="StringComparison.OrdinalIgnoreCase" />.
        /// </summary>
        /// <param name="strA">String A. Can be <see langword="null"/>.</param>
        /// <param name="strB">String B. Can be <see langword="null"/>.</param>
        /// <returns><see langword="true"/>, if the first string is equal to the second string; otherwise, <see langword="false"/>.</returns>
        public static bool IsEqualIgnoreCase(this string strA, string strB)
            => string.Compare(strA, strB, StringComparison.OrdinalIgnoreCase) == 0;

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
        public static ChunkSequence<char> Split(this string str, int chunkSize) => new ChunkSequence<char>(str.AsMemory(), chunkSize);

        /// <summary>
        /// Gets managed pointer to the first character in the string.
        /// </summary>
        /// <param name="str">The string data.</param>
        /// <returns>The managed pointer to the first character in the string.</returns>
        public static ref readonly char GetRawData(this string str)
        {
            const string pinnedString = "pinnedStr";
            const string methodExit = "exit";
            DeclareLocals(true, new Var(pinnedString, typeof(string)).Pinned());
            Push(str);
            Stloc(pinnedString);
            Ldloc(pinnedString);
            Conv_U();
            Dup();
            Brfalse(methodExit);
            Call(M.PropertyGet(typeof(RuntimeHelpers), nameof(RuntimeHelpers.OffsetToStringData)));
            Conv_U();
            Add();
            MarkLabel(methodExit);
            return ref ReturnRef<char>();
        }

        /// <summary>
        /// Converts string into base64 representation.
        /// </summary>
        /// <param name="value">The value to be converted to base64.</param>
        /// <param name="encoding">The encoding used to convert the string to bytes.</param>
        /// <returns>The base64 representation of the string.</returns>
        public static string ToBase64(this string value, Encoding encoding)
        {
            var bytesCount = value.Length * encoding.GetMaxByteCount(1);
            using MemoryRental<byte> buffer = bytesCount <= 1024 ? stackalloc byte[bytesCount] : new MemoryRental<byte>(bytesCount);
            bytesCount = encoding.GetBytes(value.AsSpan(), buffer.Span);
            return Convert.ToBase64String(buffer.Span.Slice(0, bytesCount));
        }

        /// <summary>
        /// Decodes string from its base64 representation.
        /// </summary>
        /// <param name="base64"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static string FromBase64(this string base64, Encoding encoding)
        {
            using MemoryRental<byte> buffer = base64.Length <= 1024 ? stackalloc byte[base64.Length] : new MemoryRental<byte>(base64.Length);
            var converted = Convert.TryFromBase64String(base64, buffer.Span, out var count);
            Debug.Assert(converted);
            using MemoryRental<char> chars = count <= 1024 ? stackalloc char[count] : new MemoryRental<char>(count);
            count = encoding.GetChars(buffer.Span.Slice(0, count), chars.Span);
            return new string(chars.Span.Slice(0, count));
        }
    }
}