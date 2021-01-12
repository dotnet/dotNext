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
            using (var ms = new MemoryStream())
            {
                ms.Write(encoding.GetBytes(helloWorld));
                ms.Position = 0;
                var buffer = new byte[bufferSize];
                Equal(helloWorld, ms.ReadString(encoding.GetByteCount(helloWorld), encoding, buffer));
            }
        }

        [Theory]
        [InlineData(4)]
        [InlineData(15)]
        [InlineData(128)]
        public static void ReadString(int bufferSize)
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
            using (var ms = new MemoryStream())
            {
                await ms.WriteAsync(encoding.GetBytes(helloWorld));
                ms.Position = 0;
                var buffer = new byte[bufferSize];
                Equal(helloWorld, await ms.ReadStringAsync(encoding.GetByteCount(helloWorld), encoding, buffer));
            }
        }

        [Theory]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(15)]
        [InlineData(1024)]
        public static async Task ReadStringAsync(int bufferSize)
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
            using (var ms = new MemoryStream())
            {
                var buffer = new byte[bufferSize];
                ms.WriteString(helloWorld, encoding, buffer);
                ms.Position = 0;
                Equal(helloWorld, ms.ReadString(encoding.GetByteCount(helloWorld), encoding, buffer));
            }
        }

        [Theory]
        [InlineData(10)]
        [InlineData(15)]
        [InlineData(1024)]
        public static void ReadWriteString(int bufferSize)
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
            using (var ms = new MemoryStream())
            {
                var buffer = new byte[bufferSize];
                await ms.WriteStringAsync(helloWorld, encoding, buffer);
                ms.Position = 0;
                Equal(helloWorld, await ms.ReadStringAsync(encoding.GetByteCount(helloWorld), encoding, buffer));
            }
        }

        [Theory]
        [InlineData(10)]
        [InlineData(15)]
        [InlineData(1024)]
        public static async Task ReadWriteStringAsync(int bufferSize)
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
            using (var source = new MemoryStream(new byte[] { 2, 4, 5 }))
            using (var destination = new MemoryStream())
            {
                var buffer = new byte[2];
                Equal(3L, source.CopyTo(destination, buffer));
            }
        }
    }
}