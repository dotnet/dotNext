using System.Net.Mime;
using System.Text;

namespace DotNext.Net.Mime;

using Text;

/// <summary>
/// Represents extension methods for <see cref="ContentType"/> data type.
/// </summary>
public static class ContentTypeExtensions
{
    /// <summary>
    /// Extends <see cref="ContentType"/> type.
    /// </summary>
    /// <param name="contentType">The value to extend.</param>
    extension(ContentType contentType)
    {
        /// <summary>
        /// Gets text encoding specified by media type.
        /// </summary>
        /// <returns>The encoding specified by <paramref name="contentType"/>.</returns>
        public Encoding Encoding => (contentType.CharSet is { Length: > 0 } charSet
            ? Encoding.GetEncoding(charSet)
            : Encoding.UTF8).WithoutPreamble;
    }
}