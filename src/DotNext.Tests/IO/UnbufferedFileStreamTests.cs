namespace DotNext.IO;

public sealed class UnbufferedFileStreamTests : Test
{
    [Fact]
    public static void ReadWriteSynchronously()
    {
        var fileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(fileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, FileOptions.DeleteOnClose);
        using var stream = handle.AsUnbufferedStream(FileAccess.ReadWrite);
        True(stream.CanRead);
        True(stream.CanWrite);
        True(stream.CanSeek);
        Equal(0L, stream.Position);
        Equal(0L, stream.Length);

        var expected = new byte[] { 10, 20, 30 };
        stream.SetLength(expected.Length);
        Equal(3L, stream.Length);
        Equal(0L, stream.Position);

        stream.Write(expected, 0, expected.Length);
        Equal(3L, stream.Position);
        stream.Flush();

        stream.Position = 0L;
        var actual = new byte[expected.Length];
        Equal(3, stream.Read(actual, 0, actual.Length));
        Equal(3L, stream.Position);

        Equal(expected, actual);

        stream.Position = 0L;
        stream.WriteByte(42);
        stream.Seek(-1L, SeekOrigin.Current);
        Equal(42, stream.ReadByte());
    }

    [Fact]
    public static async Task ReadWriteAsynchronously()
    {
        var fileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(fileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, FileOptions.DeleteOnClose);
        await using var stream = handle.AsUnbufferedStream(FileAccess.ReadWrite);
        True(stream.CanRead);
        True(stream.CanWrite);
        True(stream.CanSeek);
        Equal(0L, stream.Position);
        Equal(0L, stream.Length);

        var expected = new byte[] { 10, 20, 30 };
        stream.SetLength(expected.Length);
        Equal(3L, stream.Length);
        Equal(0L, stream.Position);

        await stream.WriteAsync(expected);
        Equal(3L, stream.Position);
        await stream.FlushAsync(CancellationToken.None);

        stream.Seek(0L, SeekOrigin.Begin);
        var actual = new byte[expected.Length];
        Equal(3, await stream.ReadAsync(actual));
        Equal(3L, stream.Position);

        Equal(expected, actual);
    }
}