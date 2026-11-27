namespace DotNext.IO;

public sealed class RandomAccessExtensionsTests : Test
{
    private static string CreateFile(int length)
    {
        var fileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var content = new byte[length];
        for (var i = 0; i < content.Length; i++)
            content[i] = (byte)i;

        File.WriteAllBytes(fileName, content);
        return fileName;
    }

    [Fact]
    public static void ReadExactly()
    {
        var fileName = CreateFile(10);
        try
        {
            using var handle = File.OpenHandle(fileName);
            var buffer = new byte[4];
            RandomAccess.ReadExactly(handle, buffer, fileOffset: 6L);
            Equal<byte>([6, 7, 8, 9], buffer);

            RandomAccess.ReadExactly(handle, Span<byte>.Empty, fileOffset: 10L);
            Throws<EndOfStreamException>(() => RandomAccess.ReadExactly(handle, new byte[5], fileOffset: 6L));
            Throws<ArgumentOutOfRangeException>(() => RandomAccess.ReadExactly(handle, buffer, fileOffset: -1L));
            Throws<ArgumentNullException>(() => RandomAccess.ReadExactly(null!, buffer, fileOffset: 0L));
        }
        finally
        {
            File.Delete(fileName);
        }
    }

    [Fact]
    public static async Task ReadExactlyAsync()
    {
        var fileName = CreateFile(10);
        try
        {
            using var handle = File.OpenHandle(fileName, options: FileOptions.Asynchronous);
            var buffer = new byte[4];
            await RandomAccess.ReadExactlyAsync(handle, buffer, fileOffset: 6L, TestToken);
            Equal<byte>([6, 7, 8, 9], buffer);

            await RandomAccess.ReadExactlyAsync(handle, Memory<byte>.Empty, fileOffset: 10L, TestToken);
            await ThrowsAsync<EndOfStreamException>(RandomAccess.ReadExactlyAsync(handle, new byte[5], fileOffset: 6L, TestToken).AsTask);
            Throws<ArgumentOutOfRangeException>(() => RandomAccess.ReadExactlyAsync(handle, buffer, fileOffset: -1L, TestToken));
            Throws<ArgumentNullException>(() => RandomAccess.ReadExactlyAsync(null!, buffer, fileOffset: 0L, TestToken));
        }
        finally
        {
            File.Delete(fileName);
        }
    }
}
