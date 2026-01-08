using System.Buffers;
using System.Text;

namespace DotNext.Text;

using Buffers;

public sealed class StringInterpolationTests : Test
{
    [Fact]
    public static void AllocateString()
    {
        int x = 10, y = 20;
        using var actual = StringInterpolation.Interpolate(MemoryAllocator<char>.Default, $"{x} + {y} = {x + y}");
        Equal($"{x} + {y} = {x + y}", actual.Span.ToString());
    }
    
    [Theory]
    [InlineData(10, 10)]
    [InlineData(int.MaxValue, int.MinValue)]
    public static void WriteInterpolatedStringToBufferWriter(int x, int y)
    {
        using var buffer = new PoolingArrayBufferWriter<char>();

        buffer.Interpolate($"{x,4:X} = {y,-3:X}");
        Equal($"{x,4:X} = {y,-3:X}", buffer.ToString());
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(int.MaxValue, int.MinValue)]
    public static async Task WriteInterpolatedStringToBufferWriterAsync(int x, int y)
    {
        var xt = Task.FromResult(x);
        var yt = Task.FromResult(y);

        using var buffer = new PoolingArrayBufferWriter<char>();
        var actualCount = buffer.Interpolate($"{await xt,4:X} = {await yt,-3:X}");
        Equal(buffer.WrittenCount, actualCount);
        Equal($"{x,4:X} = {y,-3:X}", buffer.ToString());
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(int.MaxValue, int.MinValue)]
    public static void WriteInterpolatedStringToBufferWriterSlim(int x, int y)
    {
        var buffer = new BufferWriterSlim<char>(stackalloc char[4]);
        buffer.Interpolate($"{x,4:X} = {y,-3:X}");
        Equal($"{x,4:X} = {y,-3:X}", buffer.ToString());
        buffer.Dispose();
    }

    [Theory]
    [InlineData(0, "UTF-8", 10, 10)]
    [InlineData(0, "UTF-8", int.MaxValue, int.MinValue)]
    [InlineData(0, "UTF-16LE", 10, 10)]
    [InlineData(0, "UTF-16BE", int.MaxValue, int.MinValue)]
    [InlineData(0, "UTF-32LE", 10, 10)]
    [InlineData(0, "UTF-32BE", int.MaxValue, int.MinValue)]
    [InlineData(8, "UTF-8", 10, 10)]
    [InlineData(8, "UTF-8", int.MaxValue, int.MinValue)]
    [InlineData(8, "UTF-16LE", 10, 10)]
    [InlineData(8, "UTF-16BE", int.MaxValue, int.MinValue)]
    [InlineData(8, "UTF-32LE", 10, 10)]
    [InlineData(8, "UTF-32BE", int.MaxValue, int.MinValue)]
    public static void EncodeInterpolatedStringUsingBufferWriter(int bufferSize, string encoding, int x, int y)
    {
        var writer = new ArrayBufferWriter<byte>();
        Span<char> buffer = stackalloc char[bufferSize];

        var e = Encoding.GetEncoding(encoding);
        var encoder = e.GetEncoder();
        True(writer.Interpolate(encoder, buffer, $"{x,4:X} = {y,-3:X}") > 0);

        Equal($"{x,4:X} = {y,-3:X}", e.GetString(writer.WrittenSpan));
    }
    
    [Theory]
    [InlineData(0, "UTF-8", 10, 10)]
    [InlineData(0, "UTF-8", int.MaxValue, int.MinValue)]
    [InlineData(0, "UTF-16LE", 10, 10)]
    [InlineData(0, "UTF-16BE", int.MaxValue, int.MinValue)]
    [InlineData(0, "UTF-32LE", 10, 10)]
    [InlineData(0, "UTF-32BE", int.MaxValue, int.MinValue)]
    [InlineData(8, "UTF-8", 10, 10)]
    [InlineData(8, "UTF-8", int.MaxValue, int.MinValue)]
    [InlineData(8, "UTF-16LE", 10, 10)]
    [InlineData(8, "UTF-16BE", int.MaxValue, int.MinValue)]
    [InlineData(8, "UTF-32LE", 10, 10)]
    [InlineData(8, "UTF-32BE", int.MaxValue, int.MinValue)]
    public static void EncodeInterpolatedStringUsingBufferWriterSlim(int bufferSize, string encoding, int x, int y)
    {
        var writer = new BufferWriterSlim<byte>(stackalloc byte[32]);
        Span<char> buffer = stackalloc char[bufferSize];

        var e = Encoding.GetEncoding(encoding);
        var encoder = e.GetEncoder();
        True(writer.Interpolate(encoder, buffer, $"{x,4:X} = {y,-3:X}") > 0);

        Equal($"{x,4:X} = {y,-3:X}", e.GetString(writer.WrittenSpan));
    }
    
    [Theory]
    [InlineData(0, "UTF-8", 10, 10)]
    [InlineData(0, "UTF-8", int.MaxValue, int.MinValue)]
    [InlineData(0, "UTF-16LE", 10, 10)]
    [InlineData(0, "UTF-16BE", int.MaxValue, int.MinValue)]
    [InlineData(0, "UTF-32LE", 10, 10)]
    [InlineData(0, "UTF-32BE", int.MaxValue, int.MinValue)]
    [InlineData(8, "UTF-8", 10, 10)]
    [InlineData(8, "UTF-8", int.MaxValue, int.MinValue)]
    [InlineData(8, "UTF-16LE", 10, 10)]
    [InlineData(8, "UTF-16BE", int.MaxValue, int.MinValue)]
    [InlineData(8, "UTF-32LE", 10, 10)]
    [InlineData(8, "UTF-32BE", int.MaxValue, int.MinValue)]
    public static void EncodeInterpolatedStringInline(int bufferSize, string encoding, int x, int y)
    {
        Span<char> buffer = stackalloc char[bufferSize];

        var e = Encoding.GetEncoding(encoding);
        var encoder = e.GetEncoder();
        using var actual = StringInterpolation.Interpolate(MemoryAllocator<byte>.Default, encoder, buffer, $"{x,4:X} = {y,-3:X}");

        Equal($"{x,4:X} = {y,-3:X}", e.GetString(actual.Span));
    }
}