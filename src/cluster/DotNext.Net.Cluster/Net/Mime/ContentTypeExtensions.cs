using System.Net.Mime;
using System.Text;

namespace DotNext.Net.Mime;

using static Text.EncodingExtensions;

/// <summary>
/// Represents extension methods for <see cref="ContentType"/> data type.
/// </summary>
public static class ContentTypeExtensions
{
    /// <summary>
    /// Gets text encoding specified by media type.
    /// </summary>
    /// <param name="contentType">The content type.</param>
    /// <returns>The encoding specified by <paramref name="contentType"/>.</returns>
    public static Encoding GetEncoding(this ContentType contentType)
        => (contentType.CharSet is { Length: > 0 } charSet ? Encoding.GetEncoding(charSet) : Encoding.UTF8).WithoutPreamble();
}