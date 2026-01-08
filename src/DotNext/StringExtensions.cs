using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using static System.Runtime.InteropServices.MemoryMarshal;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext;

using Buffers;

/// <summary>
/// Represents various extension methods for type <see cref="string"/>.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Extends <see cref="string"/> type.
    /// </summary>
    /// <param name="str">The string to extend.</param>
    extension(string? str)
    {
        /// <summary>
        /// Reverse string characters.
        /// </summary>
        /// <returns>The string in inverse order of characters.</returns>
        [return: NotNullIfNotNull(nameof(str))]
        public string? Reverse()
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
        /// <param name="maxLength">Maximum length.</param>
        /// <returns>Trimmed string value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is less than zero.</exception>
        [return: NotNullIfNotNull(nameof(str))]
        public string? TrimLength(int maxLength)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maxLength);

            return str is null || str.Length <= maxLength
                ? str
                : maxLength is 0
                    ? string.Empty
                    : new(str.AsSpan(0, maxLength));
        }
    }

    /// <summary>
    /// Extends <see cref="string"/> type.
    /// </summary>
    /// <param name="str">The string to extend.</param>
    extension(string str)
    {
        /// <summary>
        /// Extracts substring from the given string.
        /// </summary>
        /// <remarks>
        /// This method if useful for .NET languages without syntactic support of ranges.
        /// </remarks>
        /// <param name="range">The range of substring.</param>
        /// <returns>The part of the receiver string extracted according to the supplied range.</returns>
        public string Substring(Range range)
        {
            var (start, length) = range.GetOffsetAndLength(str.Length);
            return str.Substring(start, length);
        }
        
        /// <summary>
        /// Concatenates multiple strings.
        /// </summary>
        /// <remarks>
        /// You can use methods from <see cref="TupleExtensions"/> to emulate variadic arguments.
        /// </remarks>
        /// <param name="values">An array of strings.</param>
        /// <param name="allocator">The allocator of the concatenated string.</param>
        /// <returns>A buffer containing characters from the concatenated strings.</returns>
        /// <exception cref="OutOfMemoryException">The concatenated string is too large.</exception>
        public static MemoryOwner<char> Concat(ReadOnlySpan<string?> values, MemoryAllocator<char>? allocator)
        {
            MemoryOwner<char> result;

            switch (values.Length)
            {
                default:
                    var totalLength = 0L;
                    foreach (var s in values)
                    {
                        if (s is { Length: > 0 })
                        {
                            totalLength += s.Length;
                        }
                    }

                    if (totalLength is 0)
                        goto case 0;

                    if (totalLength > Array.MaxLength)
                        throw new OutOfMemoryException();

                    result = allocator?.Invoke((int)totalLength) ?? new(ArrayPool<char>.Shared, (int)totalLength);
                    var output = result.Span;
                    foreach (ReadOnlySpan<char> s in values)
                    {
                        s.CopyTo(output);
                        output = output.Slice(s.Length);
                    }

                    break;
                case 0:
                    result = default;
                    break;
                case 1:
                    result = values[0].Copy(allocator);
                    break;
            }

            return result;
        }
    }
}