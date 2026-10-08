using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace DotNext.Text;

/// <summary>
/// Represents extensions for <see cref="CompositeFormat"/> type.
/// </summary>
public static class CompositeFormatExtensions
{
    private static readonly ConcurrentDictionary<string, CompositeFormat> Formats = new();
    
    extension(CompositeFormat)
    {
        /// <summary>
        /// If <paramref name="format"/> is a string literal, returns a composite format represents
        /// parsed string; otherwise, <see langword="false"/>.
        /// </summary>
        /// <param name="format">The string that describes the format.</param>
        /// <returns>Parsed format, if <paramref name="format"/> is an interned string; otherwise, <see langword="null"/>.</returns>
        public static CompositeFormat? IsInterned([StringSyntax("CompositeFormat")] string format)
            => string.IsInterned(format) is { } interned
                ? Formats.GetOrAdd(interned, CompositeFormat.Parse)
                : null;
    }
}