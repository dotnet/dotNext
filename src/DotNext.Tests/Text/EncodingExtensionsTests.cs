using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Xunit;

namespace DotNext.Text
{
    [ExcludeFromCodeCoverage]
    public sealed class EncodingExtensionsTests : Test
    {
        [Fact]
        public static void ByteOrderMark()
        {
            var withoutPreamble = Encoding.UTF8.WithoutPreamble();
            Empty(withoutPreamble.GetPreamble());
            Equal(Encoding.UTF8.BodyName, withoutPreamble.BodyName);
            Equal(Encoding.UTF8.IsAlwaysNormalized(), withoutPreamble.IsAlwaysNormalized());
            Equal(Encoding.UTF8.EncodingName, withoutPreamble.EncodingName);
            Equal(Encoding.UTF8.WebName, withoutPreamble.WebName);
            Equal(Encoding.UTF8.GetByteCount("Hello"), withoutPreamble.GetByteCount("Hello"));
            Equal(0, withoutPreamble.Preamble.Length);
            Equal(Encoding.UTF8.IsBrowserDisplay, withoutPreamble.IsBrowserDisplay);
            Equal(Encoding.UTF8.IsBrowserSave, withoutPreamble.IsBrowserSave);
            Equal(Encoding.UTF8.IsSingleByte, withoutPreamble.IsSingleByte);
            Equal(Encoding.UTF8.IsMailNewsSave, withoutPreamble.IsMailNewsSave);
        }

        [Theory]
        [InlineData("UTF-8")]
        [InlineData("UTF-7")]
        [InlineData("UTF-32LE")]
        [InlineData("UTF-32BE")]
        [InlineData("UTF-16LE")]
        [InlineData("UTF-16BE")]
        public static void EncodeDecode(string encodingName)
        {
            const string text = "Hello, world! Привет, мир! #@%^&*()";

            Encoding enc = Encoding.GetEncoding(encodingName);
            using var bytes = enc.GetBytes(text.AsSpan());
            False(bytes.IsEmpty);

            using var chars = enc.GetChars(bytes.Memory.Span);
            False(chars.IsEmpty);
            Equal(text, new string(chars.Memory.Span));
        }
    }
}
