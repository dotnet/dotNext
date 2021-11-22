using System.Diagnostics.CodeAnalysis;

namespace DotNext.IO
{
    [ExcludeFromCodeCoverage]
    public sealed class FileReaderTests : Test
    {
        [Fact]
        public static async Task SimpleReadAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, FileOptions.Asynchronous);
            using var reader = new FileReader(handle);
            False(reader.HasBufferedData);
            True(reader.Buffer.IsEmpty);

            var expected = RandomBytes(512);
            await RandomAccess.WriteAsync(handle, expected, 0L);

            True(await reader.ReadAsync());
            True(reader.HasBufferedData);
            False(reader.Buffer.IsEmpty);

            Equal(expected, reader.Buffer.ToArray());
        }

        [Fact]
        public static async Task ReadBufferTwiceAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, FileOptions.Asynchronous);
            using var reader = new FileReader(handle, bufferSize: 32);

            var expected = RandomBytes(reader.MaxBufferSize * 2);
            await RandomAccess.WriteAsync(handle, expected, 0L);

            True(await reader.ReadAsync());
            Equal(expected.AsMemory(0, reader.Buffer.Length).ToArray(), reader.Buffer.ToArray());

            reader.Consume(16);

            True(await reader.ReadAsync());
            Equal(expected.AsMemory(16, reader.Buffer.Length).ToArray(), reader.Buffer.ToArray());

            reader.Consume(16);
            True(await reader.ReadAsync());

            reader.Consume(16);
            False(await reader.ReadAsync());

            reader.ClearBuffer();
        }

        [Fact]
        public static async Task ReadLargeDataAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, FileOptions.Asynchronous);
            using var reader = new FileReader(handle, bufferSize: 32);

            var expected = RandomBytes(reader.MaxBufferSize * 2);
            await RandomAccess.WriteAsync(handle, expected, 0L);

            True(await reader.ReadAsync());
            Equal(expected.AsMemory(0, reader.Buffer.Length).ToArray(), reader.Buffer.ToArray());

            var actual = new byte[expected.Length];
            Equal(actual.Length, await reader.ReadAsync(actual));

            Equal(expected, actual);

            False(await reader.ReadAsync());
        }

        [Fact]
        public static void SimpleRead()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, FileOptions.Asynchronous);
            using var reader = new FileReader(handle);
            False(reader.HasBufferedData);
            True(reader.Buffer.IsEmpty);

            var expected = RandomBytes(512);
            RandomAccess.Write(handle, expected, 0L);

            True(reader.Read());
            True(reader.HasBufferedData);
            False(reader.Buffer.IsEmpty);

            Equal(expected, reader.Buffer.ToArray());
        }

        [Fact]
        public static void ReadBufferTwice()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, FileOptions.Asynchronous);
            using var reader = new FileReader(handle, bufferSize: 32);

            var expected = RandomBytes(reader.MaxBufferSize * 2);
            RandomAccess.Write(handle, expected, 0L);

            True(reader.Read());
            Equal(expected.AsMemory(0, reader.Buffer.Length).ToArray(), reader.Buffer.ToArray());

            reader.Consume(16);

            True(reader.Read());
            Equal(expected.AsMemory(16, reader.Buffer.Length).ToArray(), reader.Buffer.ToArray());

            reader.Consume(16);
            True(reader.Read());

            reader.Consume(16);
            False(reader.Read());

            reader.ClearBuffer();
        }

        [Fact]
        public static void ReadLargeData()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, FileOptions.Asynchronous);
            using var reader = new FileReader(handle, bufferSize: 32);

            var expected = RandomBytes(reader.MaxBufferSize * 2);
            RandomAccess.Write(handle, expected, 0L);

            True(reader.Read());
            Equal(expected.AsMemory(0, reader.Buffer.Length).ToArray(), reader.Buffer.ToArray());

            var actual = new byte[expected.Length];
            Equal(actual.Length, reader.Read(actual));

            Equal(expected, actual);

            False(reader.Read());
        }
    }
}