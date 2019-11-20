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
        private static void ReadStringUsingEncoding(Encoding encoding, int bufferSize)
        {
            const string helloWorld = "Hello, world! &$@&@()&$YHWORww!";
            using var ms = new MemoryStream();
            ms.Write(encoding.GetBytes(helloWorld));
            ms.Position = 0;
            var buffer = new byte[bufferSize];
            Equal(helloWorld, ms.ReadString(encoding.GetByteCount(helloWorld), encoding, buffer));
        }

        private static void ReadStringUsingEncoding(Encoding encoding)
        {
            const string helloWorld = "Hello, world! &$@&@()&$YHWORww!";
            using var ms = new MemoryStream();
            ms.Write(encoding.GetBytes(helloWorld));
            ms.Position = 0;
            Equal(helloWorld, ms.ReadString(encoding.GetByteCount(helloWorld), encoding));
        }

        [Fact]
        public static void ReadString()
        {
            ReadStringUsingEncoding(Encoding.UTF8);
            ReadStringUsingEncoding(Encoding.Unicode);
            ReadStringUsingEncoding(Encoding.UTF7);
            ReadStringUsingEncoding(Encoding.UTF32);
            ReadStringUsingEncoding(Encoding.ASCII);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(15)]
        [InlineData(128)]
        public static void ReadStringBuffered(int bufferSize)
        {
            ReadStringUsingEncoding(Encoding.UTF8, bufferSize);
            ReadStringUsingEncoding(Encoding.Unicode, bufferSize);
            ReadStringUsingEncoding(Encoding.UTF7, bufferSize);
            ReadStringUsingEncoding(Encoding.UTF32, bufferSize);
            ReadStringUsingEncoding(Encoding.ASCII, bufferSize);
        }

        private static async Task ReadStringUsingEncodingAsync(Encoding encoding, int bufferSize)
        {
            const string helloWorld = "Hello, world! $(@$)Hjdqgd!";
            using var ms = new MemoryStream();
            await ms.WriteAsync(encoding.GetBytes(helloWorld));
            ms.Position = 0;
            var buffer = new byte[bufferSize];
            Equal(helloWorld, await ms.ReadStringAsync(encoding.GetByteCount(helloWorld), encoding, buffer));
        }

        private static async Task ReadStringUsingEncodingAsync(Encoding encoding)
        {
            const string helloWorld = "Hello, world! $(@$)Hjdqgd!";
            using var ms = new MemoryStream();
            await ms.WriteAsync(encoding.GetBytes(helloWorld));
            ms.Position = 0;
            Equal(helloWorld, await ms.ReadStringAsync(encoding.GetByteCount(helloWorld), encoding));
        }

        [Fact]
        public static async Task ReadStringAsync()
        {
            await ReadStringUsingEncodingAsync(Encoding.UTF8);
            await ReadStringUsingEncodingAsync(Encoding.Unicode);
            await ReadStringUsingEncodingAsync(Encoding.UTF7);
            await ReadStringUsingEncodingAsync(Encoding.UTF32);
            await ReadStringUsingEncodingAsync(Encoding.ASCII);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(15)]
        [InlineData(1024)]
        public static async Task ReadStringBufferedAsync(int bufferSize)
        {
            await ReadStringUsingEncodingAsync(Encoding.UTF8, bufferSize);
            await ReadStringUsingEncodingAsync(Encoding.Unicode, bufferSize);
            await ReadStringUsingEncodingAsync(Encoding.UTF7, bufferSize);
            await ReadStringUsingEncodingAsync(Encoding.UTF32, bufferSize);
            await ReadStringUsingEncodingAsync(Encoding.ASCII, bufferSize);
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

        private static void ReadWriteStringUsingEncoding(Encoding encoding)
        {
            const string helloWorld = "Hello, world!&*(@&*(fghjwgfwffgw";
            using var ms = new MemoryStream();
            ms.WriteString(helloWorld, encoding);
            ms.Position = 0;
            Equal(helloWorld, ms.ReadString(encoding.GetByteCount(helloWorld), encoding));
        }

        [Fact]
        public static void ReadWriteString()
        {
            ReadWriteStringUsingEncoding(Encoding.UTF8);
            ReadWriteStringUsingEncoding(Encoding.Unicode);
            ReadWriteStringUsingEncoding(Encoding.UTF7);
            ReadWriteStringUsingEncoding(Encoding.UTF32);
            ReadWriteStringUsingEncoding(Encoding.ASCII);
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

        private static async Task ReadWriteStringUsingEncodingAsync(Encoding encoding, int bufferSize)
        {
            const string helloWorld = "Hello, world!&*(@&*(fghjwgfwffgw";
            using var ms = new MemoryStream();
            var buffer = new byte[bufferSize];
            await ms.WriteStringAsync(helloWorld.AsMemory(), encoding, buffer);
            ms.Position = 0;
            Equal(helloWorld, await ms.ReadStringAsync(encoding.GetByteCount(helloWorld), encoding, buffer));
        }

        private static async Task ReadWriteStringUsingEncodingAsync(Encoding encoding)
        {
            const string helloWorld = "Hello, world!&*(@&*(fghjwgfwffgw";
            using var ms = new MemoryStream();
            await ms.WriteStringAsync(helloWorld.AsMemory(), encoding);
            ms.Position = 0;
            Equal(helloWorld, await ms.ReadStringAsync(encoding.GetByteCount(helloWorld), encoding));
        }

        [Fact]
        public static async Task ReadWriteStringAsync()
        {
            await ReadWriteStringUsingEncodingAsync(Encoding.UTF8);
            await ReadWriteStringUsingEncodingAsync(Encoding.Unicode);
            await ReadWriteStringUsingEncodingAsync(Encoding.UTF7);
            await ReadWriteStringUsingEncodingAsync(Encoding.UTF32);
            await ReadWriteStringUsingEncodingAsync(Encoding.ASCII);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(15)]
        [InlineData(1024)]
        public static async Task ReadWriteBufferedStringAsync(int bufferSize)
        {
            await ReadWriteStringUsingEncodingAsync(Encoding.UTF8, bufferSize);
            await ReadWriteStringUsingEncodingAsync(Encoding.Unicode, bufferSize);
            await ReadWriteStringUsingEncodingAsync(Encoding.UTF7, bufferSize);
            await ReadWriteStringUsingEncodingAsync(Encoding.UTF32, bufferSize);
            await ReadWriteStringUsingEncodingAsync(Encoding.ASCII, bufferSize);
        }

        [Fact]
        public static void SynchronousCopying()
        {
            using var source = new MemoryStream(new byte[] { 2, 4, 5 });
            using var destination = new MemoryStream();
            var buffer = new byte[2];
            Equal(3L, source.CopyTo(destination, buffer));
        }
    }
}