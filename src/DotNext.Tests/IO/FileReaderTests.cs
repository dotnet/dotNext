namespace DotNext.IO;

public sealed class FileReaderTests : Test
{
    [Fact]
    public static async Task SimpleReadAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var reader = new FileReader(handle);
        False(reader.HasBufferedData);
        True(reader.Buffer.IsEmpty);
        True(reader.As<IAsyncBinaryReader>().TryGetRemainingBytesCount(out var remainingCount));
        Equal(0L, remainingCount);

        var expected = RandomBytes(512);
        await RandomAccess.WriteAsync(handle, expected, 0L, TestToken);

        True(await reader.ReadAsync(TestToken));
        Equal(0L, reader.FilePosition);
        Equal(expected.Length, reader.ReadPosition);
        Throws<InvalidOperationException>(() => reader.FilePosition = 10L);
        Throws<ArgumentOutOfRangeException>(() => reader.FilePosition = -10L);
        True(reader.HasBufferedData);
        False(reader.Buffer.IsEmpty);
        True(reader.As<IAsyncBinaryReader>().TryGetRemainingBytesCount(out remainingCount));
        Equal(expected.Length, remainingCount);

        Equal(expected, reader.Buffer);
    }

    [Fact]
    public static async Task ReadBufferTwiceAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var reader = new FileReader(handle) { MaxBufferSize = 32 };

        var expected = RandomBytes(reader.MaxBufferSize * 2);
        await RandomAccess.WriteAsync(handle, expected, 0L, TestToken);

        True(await reader.ReadAsync(TestToken));
        Equal(expected.AsMemory(0, reader.Buffer.Length), reader.Buffer);

        reader.Consume(16);

        True(await reader.ReadAsync(TestToken));
        Equal(expected.AsMemory(16, reader.Buffer.Length), reader.Buffer);

        reader.Consume(16);
        True(await reader.ReadAsync(TestToken));

        reader.Consume(16);
        False(await reader.ReadAsync(TestToken));

        reader.Reset();
    }

    [Fact]
    public static async Task ReadLargeDataAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var reader = new FileReader(handle) { MaxBufferSize = 32 };

        var expected = RandomBytes(reader.MaxBufferSize * 2);
        await RandomAccess.WriteAsync(handle, expected, 0L, TestToken);

        True(await reader.ReadAsync(TestToken));
        Equal(expected.AsMemory(0, reader.Buffer.Length), reader.Buffer);

        var actual = new byte[expected.Length];
        Equal(actual.Length, await reader.ReadAsync(actual, TestToken));
        Equal(expected, actual);
        False(await reader.ReadAsync(TestToken));
    }

    [Fact]
    public static void SimpleRead()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var reader = new FileReader(handle);
        False(reader.HasBufferedData);
        True(reader.Buffer.IsEmpty);

        var expected = RandomBytes(512);
        RandomAccess.Write(handle, expected, 0L);

        True(reader.Read());
        True(reader.HasBufferedData);
        False(reader.Buffer.IsEmpty);

        Equal(expected, reader.Buffer);
    }

    [Fact]
    public static void ReadBufferTwice()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var reader = new FileReader(handle) { MaxBufferSize = 32 };

        var expected = RandomBytes(reader.MaxBufferSize * 2);
        RandomAccess.Write(handle, expected, 0L);

        True(reader.Read());
        Equal(expected.AsMemory(0, reader.Buffer.Length), reader.Buffer);

        reader.Consume(16);

        True(reader.Read());
        Equal(expected.AsMemory(16, reader.Buffer.Length), reader.Buffer);

        reader.Consume(16);
        True(reader.Read());

        reader.Consume(16);
        False(reader.Read());

        reader.Reset();
    }

    [Fact]
    public static void ReadLargeData()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var reader = new FileReader(handle) { MaxBufferSize = 32 };

        var expected = RandomBytes(reader.MaxBufferSize * 2);
        RandomAccess.Write(handle, expected, 0L);

        True(reader.Read());
        Equal(expected.AsMemory(0, reader.Buffer.Length), reader.Buffer);

        var actual = new byte[expected.Length];
        Equal(actual.Length, reader.Read(actual));

        Equal(expected, actual);

        False(reader.Read());
    }

    [Fact]
    public static async Task ReadSequentially()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var reader = new FileReader(fs) { MaxBufferSize = 32 };
        var bytes = RandomBytes(1024);

        await fs.WriteAsync(bytes, TestToken);
        await fs.FlushAsync(TestToken);

        using var ms = new MemoryStream(1024);
        await foreach (var chunk in reader)
            await ms.WriteAsync(chunk, TestToken);

        Equal(bytes, ms.ToArray());
    }
}