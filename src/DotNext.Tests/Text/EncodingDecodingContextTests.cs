using System.Text;

namespace DotNext.Text;

public sealed class EncodingDecodingContextTests : Test
{
    [Fact]
    public static void EncodingContextInstantiation()
    {
        EncodingContext context = Encoding.UTF8;
        Equal(Encoding.UTF8, context.Encoding);
        object clone = ((ICloneable)context).Clone();
        IsType<EncodingContext>(clone);
        Same(context.Encoding, ((EncodingContext)clone).Encoding);
    }

    [Fact]
    public static void DecodingContextInstantiation()
    {
        DecodingContext context = Encoding.UTF8;
        Equal(Encoding.UTF8, context.Encoding);
        object clone = ((ICloneable)context).Clone();
        IsType<DecodingContext>(clone);
        Same(context.Encoding, ((DecodingContext)clone).Encoding);
    }

    [Theory]
    [InlineData("UTF-8", 1)]
    [InlineData("UTF-8", 4)]
    [InlineData("UTF-8", 8)]
    [InlineData("UTF-8", 128)]
    [InlineData("UTF-16LE", 1)]
    [InlineData("UTF-16LE", 4)]
    [InlineData("UTF-16LE", 8)]
    [InlineData("UTF-16LE", 128)]
    [InlineData("UTF-16BE", 1)]
    [InlineData("UTF-16BE", 4)]
    [InlineData("UTF-16BE", 8)]
    [InlineData("UTF-16BE", 128)]
    [InlineData("UTF-32LE", 1)]
    [InlineData("UTF-32LE", 4)]
    [InlineData("UTF-32LE", 8)]
    [InlineData("UTF-32LE", 128)]
    [InlineData("UTF-32BE", 1)]
    [InlineData("UTF-32BE", 4)]
    [InlineData("UTF-32BE", 8)]
    [InlineData("UTF-32BE", 128)]
    public static void EncodeDecode(string encodingName, int charBufferSize)
    {
        const string value = "Hello, world!&*(@&*(fghjwgfwffgw Привет, мир!";

        var encoding = Encoding.GetEncoding(encodingName);
        using var ms = new MemoryStream();

        // encode
        var bytesBuffer = new byte[16];
        foreach (var chunk in new EncodingContext(encoding, true).GetBytes(value.AsMemory(), bytesBuffer))
            ms.Write(chunk.Span);

        // decode
        var sb = new StringBuilder(charBufferSize);
        var charBuffer = new char[charBufferSize];

        foreach (var chunk in new DecodingContext(encoding, true).GetChars(ms.ToArray(), charBuffer))
            sb.Append(chunk);

        Equal(value, sb.ToString());
    }
}