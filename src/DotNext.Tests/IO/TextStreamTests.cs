using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using static System.Globalization.CultureInfo;

namespace DotNext.IO
{
    using Buffers;

    [ExcludeFromCodeCoverage]
    public sealed class TextStreamTests : Test
    {
        [Fact]
        public static void WriteText()
        {
            using var writer = new PooledArrayBufferWriter<char>();
            using var actual = writer.AsTextWriter();
            
            using TextWriter expected = new StringWriter(InvariantCulture);

            actual.Write("Hello, world!");
            expected.Write("Hello, world!");

            actual.Write("123".AsSpan());
            expected.Write("123".AsSpan());

            actual.Write(TimeSpan.Zero);
            expected.Write(TimeSpan.Zero);

            actual.Write(true);
            expected.Write(true);

            actual.Write('a');
            expected.Write('a');

            actual.Write(20);
            expected.Write(20);

            actual.Write(20U);
            expected.Write(20U);

            actual.Write(42L);
            expected.Write(42L);

            actual.Write(46UL);
            expected.Write(46UL);

            actual.Write(89M);
            expected.Write(89M);

            actual.Write(78.8F);
            expected.Write(78.8F);

            actual.Write(90.9D);
            expected.Write(90.9D);

            actual.WriteLine();
            expected.WriteLine();

            actual.Flush();
            Equal(expected.ToString(), writer.ToString());
            Equal(expected.ToString(), actual.ToString());
        }

        [Fact]
        public static async Task WriteTextAsync()
        {
            var writer = new ArrayBufferWriter<char>();
            using var actual = writer.AsTextWriter();
            
            using TextWriter expected = new StringWriter(InvariantCulture);

            await actual.WriteAsync("Hello, world!");
            await expected.WriteAsync("Hello, world!");

            await actual.WriteAsync("123".AsMemory());
            await expected.WriteAsync("123".AsMemory());

            await actual.WriteAsync('a');
            await expected.WriteAsync('a');

            await actual.WriteLineAsync();
            await expected.WriteLineAsync();

            await actual.FlushAsync();
            Equal(expected.ToString(), writer.BuildString());
            Equal(expected.ToString(), actual.ToString());
        }

        [Fact]
        public static void BufferWriterOverText()
        {
            using var builder = new StringWriter();
            using var writer = builder.AsBufferWriter();
            writer.Write("Hello, ");
            writer.Write("world!");
            True(builder.GetStringBuilder().Length == 0);
            writer.Flush(false);
            Equal("Hello, world!", builder.ToString());
        }
    }
}