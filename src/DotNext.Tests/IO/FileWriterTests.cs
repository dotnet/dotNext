using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace DotNext.IO;

using Buffers;
using Buffers.Binary;

public sealed class FileWriterTests : Test
{
    [Fact]
    public static async Task WriteWithoutOverflowAsync()
    {
        var path = GetTempPath();
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var writer = new FileWriter(handle) { MaxBufferSize = 64 };
        False(writer.HasBufferedData);
        Equal(0L, writer.FilePosition);
        Equal(0L, writer.WritePosition);

        var expected = RandomBytes(32);
        await writer.WriteAsync(expected, TestToken);
        True(writer.HasBufferedData);
        Equal(0L, writer.FilePosition);
        Equal(expected.Length, writer.WritePosition);

        await writer.As<IFlushable>().FlushAsync(TestToken);
        Equal(expected.Length, writer.FilePosition);

        var actual = new byte[expected.Length];
        await RandomAccess.ReadAsync(handle, actual, 0L, TestToken);

        Equal(expected, actual);
    }

    [Fact]
    public static async Task WriteWithOverflowAsync()
    {
        var path = GetTempPath();
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var writer = new FileWriter(handle) { MaxBufferSize = 64 };

        var expected = RandomBytes(writer.Buffer.Length + 10);
        await writer.WriteAsync(expected, TestToken);
        False(writer.HasBufferedData);
        Equal(expected.Length, writer.FilePosition);

        var actual = new byte[expected.Length];
        await RandomAccess.ReadAsync(handle, actual, 0L, TestToken);

        Equal(expected, actual);
    }

    [Fact]
    public static async Task WritDirectAsync()
    {
        var path = GetTempPath();
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var writer = new FileWriter(handle) { MaxBufferSize = 64 };

        var expected = RandomBytes(writer.Buffer.Length << 2);
        await writer.WriteAsync(expected.AsMemory(0, 63), TestToken);
        await writer.WriteAsync(expected.AsMemory(63), TestToken);
        False(writer.HasBufferedData);
        Equal(expected.Length, writer.FilePosition);

        var actual = new byte[expected.Length];
        await RandomAccess.ReadAsync(handle, actual, 0L, TestToken);

        Equal(expected, actual);
    }

    [Fact]
    public static void WriteWithoutOverflow()
    {
        var path = GetTempPath();
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
        var path = GetTempPath();
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
    public static async Task FlushWithOffsetAsync()
    {
        var path = GetTempPath();
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var writer = new FileWriter(handle) { FilePosition = 100L, MaxBufferSize = 64 };
        writer.Buffer.Span[0] = 1;
        writer.Buffer.Span[1] = 2;
        writer.Produce(2);
        await writer.WriteAsync(TestToken);

        var actual = new byte[102];
        await RandomAccess.ReadAsync(handle, actual, 0L, TestToken);
        Equal(2, actual[101]);
        Equal(1, actual[100]);
    }

    [Fact]
    public static async Task WriteDirect()
    {
        var path = GetTempPath();
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var writer = new FileWriter(handle) { MaxBufferSize = 64 };
        await writer.WriteAsync(new Blittable<Buffer512> { Value = default }, TestToken);
        False(writer.HasBufferedData);
        Equal(writer.FilePosition, Unsafe.SizeOf<Buffer512>());
    }

    [Fact]
    public static async Task BufferOverflow()
    {
        var path = GetTempPath();
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var writer = new FileWriter(handle) { MaxBufferSize = 64 };
        await writer.WriteAsync(new byte[2], TestToken);
        await writer.WriteAsync(new Blittable<Buffer512> { Value = default }, TestToken);
        False(writer.HasBufferedData);
        Equal(writer.FilePosition, Unsafe.SizeOf<Buffer512>() + 2);
    }

    [Fact]
    public static void TryWrite()
    {
        var path = GetTempPath();
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, FileOptions.DeleteOnClose);
        using var writer = new FileWriter(handle) { MaxBufferSize = 64 };

        True(writer.TryWrite(new byte[2]));
        True(writer.HasBufferedData);
        Equal("\0\0"u8, writer.WrittenBuffer.Span);
    }

    private static FileWriter CreateWriter(out SafeFileHandle handle, MemoryAllocator<byte> allocator = null)
    {
        handle = File.OpenHandle(GetTempPath(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        return new(handle) { MaxBufferSize = 16, Allocator = allocator };
    }

    private static byte[] ReadAll(SafeFileHandle handle)
    {
        var result = new byte[RandomAccess.GetLength(handle)];
        RandomAccess.ReadExactly(handle, result, 0L);
        return result;
    }

    [Fact]
    public static async Task FormatUtf8WhenBufferIsAlmostFull()
    {
        using var writer = CreateWriter(out var handle);
        using (handle)
        {
            writer.Write(new byte[14]);
            Equal(6, await writer.FormatAsync(42, LengthFormat.LittleEndian, token: TestToken));
            await writer.WriteAsync(TestToken);

            Equal([.. new byte[14], 2, 0, 0, 0, (byte)'4', (byte)'2'], ReadAll(handle));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(LengthFormat.LittleEndian)]
    [InlineData(LengthFormat.Compressed)]
    public static async Task FormatUtf8LargerThanBuffer(LengthFormat? lengthFormat)
    {
        var maxAllocation = 0;
        MemoryAllocator<byte> allocator = length =>
        {
            maxAllocation = int.Max(maxAllocation, length);
            return new(new byte[length]);
        };

        var value = Guid.NewGuid();
        using var writer = CreateWriter(out var handle, allocator);
        using (handle)
        {
            var bytesWritten = await writer.FormatAsync(value, lengthFormat, "N", token: TestToken);
            await writer.WriteAsync(TestToken);

            var actual = ReadAll(handle);
            Equal(actual.Length, bytesWritten);
            Equal(Encoding.UTF8.GetBytes(value.ToString("N")), actual.AsSpan(actual.Length - 32).ToArray());
            InRange(maxAllocation, 0, 1024);
        }
    }

    [Fact]
    public static async Task EncodeCharLargerThanBuffer()
    {
        const string value = "ab\uD800cd";
        var encoding = Encoding.GetEncoding("utf-8", new EncoderReplacementFallback("???"), DecoderFallback.ReplacementFallback);
        True(encoding.GetMaxByteCount(1) > 16);

        using var writer = CreateWriter(out var handle);
        using (handle)
        {
            var bytesWritten = await writer.EncodeAsync(value.AsMemory(), new(encoding, reuseEncoder: true), LengthFormat.Compressed, TestToken)
                .AsTask()
                .WaitAsync(DefaultTimeout, TestToken);
            await writer.WriteAsync(TestToken);

            var expected = encoding.GetBytes(value);
            Equal([(byte)expected.Length, .. expected], ReadAll(handle));
            Equal(expected.Length + 1L, bytesWritten);
        }
    }

    [Fact]
    public static async Task FormatUtf8UsingInterface()
    {
        using var writer = CreateWriter(out var handle);
        using (handle)
        {
            IAsyncBinaryWriter binaryWriter = writer;
            Equal(6, await binaryWriter.FormatAsync(12345, LengthFormat.Compressed, token: TestToken));
            Equal([5, .. "12345"u8], writer.WrittenBuffer.ToArray());
        }
    }

    [Fact]
    public static async Task AdvanceWritesFullBufferOnly()
    {
        using var writer = CreateWriter(out var handle);
        using (handle)
        {
            IAsyncBinaryWriter binaryWriter = writer;
            binaryWriter.Buffer.Span[0] = 1;
            await binaryWriter.AdvanceAsync(1, TestToken);
            True(writer.HasBufferedData);
            Equal(0L, RandomAccess.GetLength(handle));

            await binaryWriter.AdvanceAsync(binaryWriter.Buffer.Length, TestToken);
            False(writer.HasBufferedData);
            Equal(16L, RandomAccess.GetLength(handle));
            Equal(16L, writer.FilePosition);
        }
    }

    [InlineArray(512)]
    private struct Buffer512
    {
        private byte element0;
    }
}