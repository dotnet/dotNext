using System.Buffers;
using System.Runtime.CompilerServices;

namespace DotNext.IO;

using Buffers.Binary;

public sealed class FileWriterTests : Test
{
    [Fact]
    public static async Task WriteWithoutOverflowAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var writer = new FileWriter(handle) { MaxBufferSize = 64 };
        False(writer.HasBufferedData);
        Equal(0L, writer.FilePosition);

        var expected = RandomBytes(32);
        await writer.WriteAsync(expected);
        True(writer.HasBufferedData);
        Equal(0L, writer.FilePosition);

        await writer.As<IFlushable>().FlushAsync();
        Equal(expected.Length, writer.FilePosition);

        var actual = new byte[expected.Length];
        await RandomAccess.ReadAsync(handle, actual, 0L);

        Equal(expected, actual);
    }

    [Fact]
    public static async Task WriteWithOverflowAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var writer = new FileWriter(handle) { MaxBufferSize = 64 };

        var expected = RandomBytes(writer.Buffer.Length + 10);
        await writer.WriteAsync(expected);
        False(writer.HasBufferedData);
        Equal(expected.Length, writer.FilePosition);

        var actual = new byte[expected.Length];
        await RandomAccess.ReadAsync(handle, actual, 0L);

        Equal(expected, actual);
    }

    [Fact]
    public static async Task WritDirectAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var writer = new FileWriter(handle) { MaxBufferSize = 64 };

        var expected = RandomBytes(writer.Buffer.Length << 2);
        await writer.WriteAsync(expected.AsMemory(0, 63));
        await writer.WriteAsync(expected.AsMemory(63));
        False(writer.HasBufferedData);
        Equal(expected.Length, writer.FilePosition);

        var actual = new byte[expected.Length];
        await RandomAccess.ReadAsync(handle, actual, 0L);

        Equal(expected, actual);
    }

    [Fact]
    public static void WriteWithoutOverflow()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, FileOptions.DeleteOnClose);
        using var writer = new FileWriter(handle) { MaxBufferSize = 64 };
        False(writer.HasBufferedData);
        Equal(0L, writer.FilePosition);

        var expected = RandomBytes(32);
        writer.Write(expected);
        True(writer.HasBufferedData);
        Equal(0L, writer.FilePosition);

        writer.As<IFlushable>().Flush();
        Equal(expected.Length, writer.FilePosition);

        var actual = new byte[expected.Length];
        RandomAccess.Read(handle, actual, 0L);

        Equal(expected, actual);
    }

    [Fact]
    public static void WriteWithOverflow()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, FileOptions.DeleteOnClose);
        using var writer = new FileWriter(handle) { MaxBufferSize = 64 };

        var expected = RandomBytes(writer.Buffer.Length + 10);
        writer.Write(expected);
        False(writer.HasBufferedData);
        Equal(expected.Length, writer.FilePosition);

        var actual = new byte[expected.Length];
        RandomAccess.Read(handle, actual, 0L);

        Equal(expected, actual);
    }

    [Fact]
    public static void WriteUsingBufferWriter()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
        using var writer = new FileWriter(fs) { MaxBufferSize = 64 };
        False(writer.HasBufferedData);
        Equal(0L, writer.FilePosition);

        var expected = RandomBytes(32);
        writer.As<IBufferWriter<byte>>().Write(expected);
        True(writer.HasBufferedData);
        Equal(0L, writer.FilePosition);

        writer.Write();
        Equal(expected.Length, writer.FilePosition);

        var actual = new byte[expected.Length];
        fs.ReadExactly(actual);

        Equal(expected, actual);
    }

    [Fact]
    public static void WriteUsingBufferWriter2()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, FileOptions.DeleteOnClose);
        using var writer = new FileWriter(handle) { MaxBufferSize = 64 };
        False(writer.HasBufferedData);
        Equal(0L, writer.FilePosition);

        var expected = RandomBytes(32);
        expected.AsMemory().CopyTo(writer.As<IBufferWriter<byte>>().GetMemory());
        writer.As<IBufferWriter<byte>>().Advance(expected.Length);
        True(writer.HasBufferedData);
        Equal(0L, writer.FilePosition);

        writer.Write();
        Equal(expected.Length, writer.FilePosition);

        var actual = new byte[expected.Length];
        RandomAccess.Read(handle, actual, 0L);

        Equal(expected, actual);
    }

    [Fact]
    public static async Task FlushWithOffsetAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var writer = new FileWriter(handle) { FilePosition = 100L, MaxBufferSize = 64 };
        writer.Buffer.Span[0] = 1;
        writer.Buffer.Span[1] = 2;
        writer.Produce(2);
        await writer.WriteAsync();

        var actual = new byte[102];
        await RandomAccess.ReadAsync(handle, actual, 0L);
        Equal(2, actual[101]);
        Equal(1, actual[100]);
    }

    [Fact]
    public static async Task WriteDirect()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var writer = new FileWriter(handle) { MaxBufferSize = 64 };
        await writer.WriteAsync(new Blittable<Buffer512> { Value = default });
        False(writer.HasBufferedData);
        Equal(writer.FilePosition, Unsafe.SizeOf<Buffer512>());
    }

    [Fact]
    public static async Task BufferOverflow()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var writer = new FileWriter(handle) { MaxBufferSize = 64 };
        await writer.WriteAsync(new byte[2]);
        await writer.WriteAsync(new Blittable<Buffer512> { Value = default });
        False(writer.HasBufferedData);
        Equal(writer.FilePosition, Unsafe.SizeOf<Buffer512>() + 2);
    }

    [Fact]
    public static void TryWrite()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, FileOptions.DeleteOnClose);
        using var writer = new FileWriter(handle) { MaxBufferSize = 64 };

        True(writer.TryWrite(new byte[2]));
        True(writer.HasBufferedData);
        Equal("\0\0"u8, writer.WrittenBuffer.Span);
    }

    [InlineArray(512)]
    private struct Buffer512
    {
        private byte element0;
    }
}