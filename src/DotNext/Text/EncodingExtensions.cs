using System.Text;

namespace DotNext.Text
{
    /// <summary>
    /// Represents extension method for <see cref="Encoding"/> data type.
    /// </summary>
    public static class EncodingExtensions
    {
        private static readonly UTF8Encoding Utf8WithoutPreamble = new UTF8Encoding(false);

        /// <summary>
        /// Returns <see cref="Encoding"/> that doesn't generate BOM.
        /// </summary>
        /// <param name="encoding">The source encoding.</param>
        /// <returns>The source encoding without BOM.</returns>
        public static Encoding WithoutPreamble(this Encoding encoding)
            => encoding is UTF8Encoding ? Utf8WithoutPreamble : EncodingWithoutPreamble.Create(encoding);
    }
}
