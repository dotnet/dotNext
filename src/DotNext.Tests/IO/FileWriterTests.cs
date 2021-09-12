using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.IO
{
    [ExcludeFromCodeCoverage]
    public sealed class FileWriterTests : Test
    {
        [Fact]
        public static async Task WriteWithoutOverflow()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, FileOptions.Asynchronous);
            using var writer = new FileWriter(handle, bufferSize: 64);
            False(writer.HasBufferedData);
            Equal(0L, writer.FilePosition);

            var expected = RandomBytes(32);
            await writer.WriteAsync(expected);
            True(writer.HasBufferedData);
            Equal(0L, writer.FilePosition);

            await writer.FlushAsync();
            Equal(expected.Length, writer.FilePosition);

            var actual = new byte[expected.Length];
            await RandomAccess.ReadAsync(handle, actual, 0L);

            Equal(expected, actual);
        }

        [Fact]
        public static async Task WriteWithOverflow()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, FileOptions.Asynchronous);
            using var writer = new FileWriter(handle, bufferSize: 64);

            var expected = RandomBytes(writer.Buffer.Length + 10);
            await writer.WriteAsync(expected);
            False(writer.HasBufferedData);
            Equal(expected.Length, writer.FilePosition);

            var actual = new byte[expected.Length];
            await RandomAccess.ReadAsync(handle, actual, 0L);

            Equal(expected, actual);
        }
    }
}