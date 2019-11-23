using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.IO
{
    [ExcludeFromCodeCoverage]
    public sealed class StreamExtensionsTests : Assert
    {
        private static void ReadStringUsingEncoding(string value, Encoding encoding, int bufferSize)
        {
            using var ms = new MemoryStream();
            ms.Write(encoding.GetBytes(value));
            ms.Position = 0;
            var buffer = new byte[bufferSize];
            Equal(value, ms.ReadString(encoding.GetByteCount(value), encoding, buffer));
        }

        private static void ReadStringUsingEncoding(string value, Encoding encoding)
        {
            using var ms = new MemoryStream();
            ms.Write(encoding.GetBytes(value));
            ms.Position = 0;
            Equal(value, ms.ReadString(encoding.GetByteCount(value), encoding));
        }

        [Fact]
        public static void ReadString()
        {
            const string testString1 = "Hello, world! &$@&@()&$YHWORww!";
            ReadStringUsingEncoding(testString1, Encoding.UTF8);
            ReadStringUsingEncoding(testString1, Encoding.Unicode);
            ReadStringUsingEncoding(testString1, Encoding.UTF7);
            ReadStringUsingEncoding(testString1, Encoding.UTF32);
            ReadStringUsingEncoding(testString1, Encoding.ASCII);
            const string testString2 = "Привет, мир!";
            ReadStringUsingEncoding(testString2, Encoding.UTF8);
            ReadStringUsingEncoding(testString2, Encoding.Unicode);
            ReadStringUsingEncoding(testString2, Encoding.UTF7);
            ReadStringUsingEncoding(testString2, Encoding.UTF32);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(15)]
        [InlineData(128)]
        public static void ReadStringBuffered(int bufferSize)
        {
            const string testString1 = "Hello, world! &$@&@()&$YHWORww!";
            ReadStringUsingEncoding(testString1, Encoding.UTF8, bufferSize);
            ReadStringUsingEncoding(testString1, Encoding.Unicode, bufferSize);
            ReadStringUsingEncoding(testString1, Encoding.UTF7, bufferSize);
            ReadStringUsingEncoding(testString1, Encoding.UTF32, bufferSize);
            ReadStringUsingEncoding(testString1, Encoding.ASCII, bufferSize);
            const string testString2 = "Привет, мир!";
            ReadStringUsingEncoding(testString2, Encoding.UTF8, bufferSize);
            ReadStringUsingEncoding(testString2, Encoding.Unicode, bufferSize);
            ReadStringUsingEncoding(testString2, Encoding.UTF7, bufferSize);
            ReadStringUsingEncoding(testString2, Encoding.UTF32, bufferSize);
        }

        private static async Task ReadStringUsingEncodingAsync(string value, Encoding encoding, int bufferSize)
        {
            using var ms = new MemoryStream();
            await ms.WriteAsync(encoding.GetBytes(value));
            ms.Position = 0;
            var buffer = new byte[bufferSize];
            Equal(value, await ms.ReadStringAsync(encoding.GetByteCount(value), encoding, buffer));
        }

        private static async Task ReadStringUsingEncodingAsync(string value, Encoding encoding)
        {
            using var ms = new MemoryStream();
            await ms.WriteAsync(encoding.GetBytes(value));
            ms.Position = 0;
            Equal(value, await ms.ReadStringAsync(encoding.GetByteCount(value), encoding));
        }

        [Fact]
        public static async Task ReadStringAsync()
        {
            const string testString1 = "Hello, world! $(@$)Hjdqgd!";
            await ReadStringUsingEncodingAsync(testString1, Encoding.UTF8);
            await ReadStringUsingEncodingAsync(testString1, Encoding.Unicode);
            await ReadStringUsingEncodingAsync(testString1, Encoding.UTF7);
            await ReadStringUsingEncodingAsync(testString1, Encoding.UTF32);
            await ReadStringUsingEncodingAsync(testString1, Encoding.ASCII);
            const string testString2 = "Привет, мир!";
            await ReadStringUsingEncodingAsync(testString2, Encoding.UTF8);
            await ReadStringUsingEncodingAsync(testString2, Encoding.Unicode);
            await ReadStringUsingEncodingAsync(testString2, Encoding.UTF7);
            await ReadStringUsingEncodingAsync(testString2, Encoding.UTF32);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(15)]
        [InlineData(1024)]
        public static async Task ReadStringBufferedAsync(int bufferSize)
        {
            const string testString1 = "Hello, world! $(@$)Hjdqgd!";
            await ReadStringUsingEncodingAsync(testString1, Encoding.UTF8, bufferSize);
            await ReadStringUsingEncodingAsync(testString1, Encoding.Unicode, bufferSize);
            await ReadStringUsingEncodingAsync(testString1, Encoding.UTF7, bufferSize);
            await ReadStringUsingEncodingAsync(testString1, Encoding.UTF32, bufferSize);
            await ReadStringUsingEncodingAsync(testString1, Encoding.ASCII, bufferSize);
            const string testString2 = "Привет, мир!";
            await ReadStringUsingEncodingAsync(testString2, Encoding.UTF8, bufferSize);
            await ReadStringUsingEncodingAsync(testString2, Encoding.Unicode, bufferSize);
            await ReadStringUsingEncodingAsync(testString2, Encoding.UTF7, bufferSize);
            await ReadStringUsingEncodingAsync(testString2, Encoding.UTF32, bufferSize);
        }

        private static void ReadWriteStringUsingEncoding(Encoding encoding, int bufferSize)
        {
            const string helloWorld = "Hello, world!&*(@&*(fghjwgfwffgw";
            using var ms = new MemoryStream();
            var buffer = new byte[bufferSize];
            ms.WriteString(helloWorld, encoding, buffer);
            ms.Position = 0;
            Equal(helloWorld, ms.ReadString(encoding.GetByteCount(helloWorld), encoding, buffer));
        }

        private static void ReadWriteStringUsingEncoding(string value, Encoding encoding)
        {
            using var ms = new MemoryStream();
            ms.WriteString(value, encoding);
            ms.Position = 0;
            Equal(value, ms.ReadString(encoding.GetByteCount(value), encoding));
        }

        [Fact]
        public static void ReadWriteString()
        {
            const string testString1 = "Hello, world!&*(@&*(fghjwgfwffgw";
            ReadWriteStringUsingEncoding(testString1, Encoding.UTF8);
            ReadWriteStringUsingEncoding(testString1, Encoding.Unicode);
            ReadWriteStringUsingEncoding(testString1, Encoding.UTF7);
            ReadWriteStringUsingEncoding(testString1, Encoding.UTF32);
            ReadWriteStringUsingEncoding(testString1, Encoding.ASCII);
            const string testString2 = "Привет, мир!";
            ReadWriteStringUsingEncoding(testString2, Encoding.UTF8);
            ReadWriteStringUsingEncoding(testString2, Encoding.Unicode);
            ReadWriteStringUsingEncoding(testString2, Encoding.UTF7);
            ReadWriteStringUsingEncoding(testString2, Encoding.UTF32);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(15)]
        [InlineData(1024)]
        public static void ReadWriteBufferedString(int bufferSize)
        {
            ReadWriteStringUsingEncoding(Encoding.UTF8, bufferSize);
            ReadWriteStringUsingEncoding(Encoding.Unicode, bufferSize);
            ReadWriteStringUsingEncoding(Encoding.UTF7, bufferSize);
            ReadWriteStringUsingEncoding(Encoding.UTF32, bufferSize);
            ReadWriteStringUsingEncoding(Encoding.ASCII, bufferSize);
        }

        private static async Task ReadWriteStringUsingEncodingAsync(string value, Encoding encoding, int bufferSize)
        {
            using var ms = new MemoryStream();
            var buffer = new byte[bufferSize];
            await ms.WriteStringAsync(value.AsMemory(), encoding, buffer);
            ms.Position = 0;
            Equal(value, await ms.ReadStringAsync(encoding.GetByteCount(value), encoding, buffer));
        }

        private static async Task ReadWriteStringUsingEncodingAsync(string value, Encoding encoding)
        {
            using var ms = new MemoryStream();
            await ms.WriteStringAsync(value.AsMemory(), encoding);
            ms.Position = 0;
            Equal(value, await ms.ReadStringAsync(encoding.GetByteCount(value), encoding));
        }

        [Fact]
        public static async Task ReadWriteStringAsync()
        {
            const string testString1 = "Hello, world!&*(@&*(fghjwgfwffgw";
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF8);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.Unicode);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF7);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF32);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.ASCII);
            const string testString2 = "Привет, мир!";
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF8);
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.Unicode);
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF7);
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF32);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(15)]
        [InlineData(1024)]
        public static async Task ReadWriteBufferedStringAsync(int bufferSize)
        {
            const string testString1 = "Hello, world!&*(@&*(fghjwgfwffgw";
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF8, bufferSize);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.Unicode, bufferSize);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF7, bufferSize);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF32, bufferSize);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.ASCII, bufferSize);
            const string testString2 = "Привет, мир!";
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF8, bufferSize);
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.Unicode, bufferSize);
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF7, bufferSize);
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF32, bufferSize);
        }

        [Fact]
        public static void SynchronousCopying()
        {
            using var source = new MemoryStream(new byte[] { 2, 4, 5 });
            using var destination = new MemoryStream();
            var buffer = new byte[2];
            Equal(3L, source.CopyTo(destination, buffer));
        }

        [Fact]
        public static void ReadWriteBlittableType()
        {
            using var ms = new MemoryStream();
            ms.Write(10M);
            ms.Position = 0;
            Equal(10M, ms.Read<decimal>());
        }

        [Fact]
        public static async Task ReadWriteBlittableTypeAsync()
        {
            using var ms = new MemoryStream();
            await ms.WriteAsync(10M);
            ms.Position = 0;
            Equal(10M, await ms.ReadAsync<decimal>());
        }
    }
}