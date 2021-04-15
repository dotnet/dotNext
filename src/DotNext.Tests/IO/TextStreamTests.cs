using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
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
        public static void WriteTextToCharBuffer()
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
        public static void EmptyLines()
        {
            var line = string.Concat(Environment.NewLine, "a", Environment.NewLine).AsMemory();
            using var reader = new ReadOnlySequence<char>(line).AsTextReader();
            Equal(string.Empty, reader.ReadLine());
            Equal("a", reader.ReadLine());
            Null(reader.ReadLine());
        }

        [Fact]
        public static void InvalidLineTermination()
        {
            var newLine = Environment.NewLine;
            var str = string.Concat("a", newLine[0].ToString());
            if (newLine.Length > 1)
            {
                using var reader = new ReadOnlySequence<char>(str.AsMemory()).AsTextReader();
                Equal(str, reader.ReadLine());
            }
        }

        [Theory]
        [InlineData("UTF-8", 16)]
        [InlineData("UTF-16BE", 16)]
        [InlineData("UTF-16LE", 16)]
        [InlineData("UTF-32BE", 16)]
        [InlineData("UTF-32LE", 16)]
        public static void DecodingWriterSparseBuffer(string encodingName, int bufferSize)
        {
            var enc = Encoding.GetEncoding(encodingName);
            var block = ToReadOnlySequence<byte>(enc.GetBytes("Hello, world!&*(@&*(fghjwgfwffgw Привет, мир!").AsMemory(), 1);
            using var reader = block.AsTextReader(enc, bufferSize);
            Equal("Hello, world!&*(@&*(fghjwgfwffgw Привет, мир!", reader.ReadToEnd());
        }

        [Theory]
        [InlineData("UTF-8", 16)]
        [InlineData("UTF-16BE", 16)]
        [InlineData("UTF-16LE", 16)]
        [InlineData("UTF-32BE", 16)]
        [InlineData("UTF-32LE", 16)]
        public static void EncodingDecodingWriter(string encodingName, int bufferSize)
        {
            using var buffer = new SparseBufferWriter<byte>(32, SparseBufferGrowth.Linear);
            var enc = Encoding.GetEncoding(encodingName);

            // write data
            using (var writer = buffer.AsTextWriter(enc, InvariantCulture))
            {
                writer.WriteLine("Привет, мир!");
                writer.WriteLine("Hello, world!&*(@&*(fghjwgfwffgw Привет, мир!");
                writer.WriteLine('c');
                writer.WriteLine(decimal.MaxValue);
                writer.WriteLine(0D);
                writer.WriteLine(1F);
                writer.WriteLine(42);
                writer.WriteLine(43U);
                writer.WriteLine(44L);
                writer.WriteLine(45UL);
                writer.WriteLine(true);
            }

            // decode data
            using (var reader = buffer.ToReadOnlySequence().AsTextReader(enc, bufferSize))
            {
                Equal('П', reader.Peek());
                Equal("Привет, мир!", reader.ReadLine());
                Equal("Hello, world!&*(@&*(fghjwgfwffgw Привет, мир!", reader.ReadLine());
                Equal('c', reader.Read());
                var newLine = Environment.NewLine;
                for (var i = 0; i < newLine.Length; i++)
                    Equal(newLine[i], reader.Read());
                Equal(decimal.MaxValue.ToString(InvariantCulture), reader.ReadLine());
                Equal(0D.ToString(InvariantCulture), reader.ReadLine());
                Equal(1F.ToString(InvariantCulture), reader.ReadLine());
                Equal(42.ToString(InvariantCulture), reader.ReadLine());
                Equal(43U.ToString(InvariantCulture), reader.ReadLine());
                Equal(44L.ToString(InvariantCulture), reader.ReadLine());
                Equal(45UL.ToString(InvariantCulture), reader.ReadLine());
                Equal(bool.TrueString, reader.ReadLine());
                Null(reader.ReadLine());
                Equal(string.Empty, reader.ReadToEnd());
                Equal(-1, reader.Peek());
                Equal(-1, reader.Read());
            }
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
        public static async Task WriteSequence()
        {
            var sequence = new [] { "abc".AsMemory(), "def".AsMemory(), "g".AsMemory() }.ToReadOnlySequence();
            await using var writer = new StringWriter();
            await writer.WriteAsync(sequence);
            Equal("abcdefg", writer.ToString());
        }
    }
}