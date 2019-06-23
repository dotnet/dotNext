using System.Text;
using System.Net.Mime;

namespace DotNext.Net.Mime
{
    using static Text.EncodingExtensions;

    public static class ContentTypeExtensions
    {
        public static Encoding GetEncoding(this ContentType contentType)
            => (string.IsNullOrEmpty(contentType.CharSet) ? Encoding.UTF8 : Encoding.GetEncoding(contentType.CharSet)).WithoutPreamble();
    }
}
