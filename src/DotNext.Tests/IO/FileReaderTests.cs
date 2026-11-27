namespace DotNext.IO;

using Buffers;

public sealed class FileReaderTests : Test
{
    [Fact]
    public static async Task SimpleReadAsync()
    {
        var path = GetTempPath();
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
        var path = GetTempPath();
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
        var path = GetTempPath();
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
        var path = GetTempPath();
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
        var path = GetTempPath();
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
        var path = GetTempPath();
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
        var path = GetTempPath();
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

    [Fact]
    public static async Task NegativeLength()
    {
        var bytes = new byte[sizeof(int) + 5];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes, -5);
        var path = GetTempPath();
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        await RandomAccess.WriteAsync(handle, bytes, 0L, TestToken);

        using (var reader = new FileReader(handle))
            await ThrowsAsync<InvalidDataException>(reader.ReadAsync(LengthFormat.LittleEndian, token: TestToken).AsTask);

        using (var reader = new FileReader(handle))
            await ThrowsAsync<InvalidDataException>(reader.DecodeAsync(System.Text.Encoding.UTF8, LengthFormat.LittleEndian, token: TestToken).AsTask);

        using (var reader = new FileReader(handle))
            await ThrowsAsync<InvalidDataException>(reader.ParseAsync<int>(LengthFormat.LittleEndian, token: TestToken).AsTask);
    }

    // a😀bcd: the 4-byte char split 3 + 1 between chunks produces a surrogate pair from 1 byte;
    // abc?defg: the dangling lead byte produces a replacement char in addition to the next chunk
    public static TheoryData<byte[]> ChunkBoundaryPayloads => new()
    {
        new byte[] { 0x61, 0xF0, 0x9F, 0x98, 0x80, 0x62, 0x63, 0x64 },
        new byte[] { 0x61, 0x62, 0x63, 0xE2, 0x64, 0x65, 0x66, 0x67 },
    };

    private static byte[] WithLengthPrefix(byte[] payload)
    {
        var result = new byte[sizeof(int) + payload.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(result, payload.Length);
        payload.CopyTo(result, sizeof(int));
        return result;
    }

    [Theory]
    [MemberData(nameof(ChunkBoundaryPayloads))]
    public static async Task DecodeInChunksAtCharBoundary(byte[] payload)
    {
        var path = GetTempPath();
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        await RandomAccess.WriteAsync(handle, WithLengthPrefix(payload), 0L, TestToken);

        using (var reader = new FileReader(handle))
        {
            var actual = new System.Text.StringBuilder();
            await foreach (var chunk in reader.DecodeAsync(System.Text.Encoding.UTF8, LengthFormat.LittleEndian, new char[4], TestToken))
                actual.Append(chunk.Span);

            Equal(System.Text.Encoding.UTF8.GetString(payload), actual.ToString());
        }

        // the empty buffer caused infinite loop
        using (var reader = new FileReader(handle))
        {
            foreach (var size in (int[])[0, 1])
            {
                await using var enumerator = reader.DecodeAsync(System.Text.Encoding.UTF8, LengthFormat.LittleEndian, new char[size], TestToken).GetAsyncEnumerator(TestToken);
                await ThrowsAsync<ArgumentException>(enumerator.MoveNextAsync().AsTask);
            }
        }
    }

    // the length prefix claims 500 MB, but the data is absent
    private static byte[] ClaimedLengthOnly()
    {
        var result = new byte[sizeof(int)];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(result, 500_000_000);
        return result;
    }

    private static byte[] LengthPrefixed(ReadOnlySpan<byte> payload)
    {
        var result = new byte[sizeof(int) + payload.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(result, payload.Length);
        payload.CopyTo(result.AsSpan(sizeof(int)));
        return result;
    }

    private sealed class AllocationTracker<T>
    {
        internal int MaxLength;

        internal MemoryAllocator<T> Allocator => length =>
        {
            MaxLength = int.Max(MaxLength, length);
            return MemoryAllocator<T>.Default(length);
        };
    }

    [Fact]
    public static async Task NoAllocationForAbsentData()
    {
        var bytes = new AllocationTracker<byte>();
        var chars = new AllocationTracker<char>();

        var path = GetTempPath();
        using var handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        await RandomAccess.WriteAsync(handle, ClaimedLengthOnly(), 0L, TestToken);

        using (var reader = new FileReader(handle))
            await ThrowsAsync<EndOfStreamException>(reader.ReadAsync(LengthFormat.LittleEndian, bytes.Allocator, TestToken).AsTask);

        using (var reader = new FileReader(handle))
            await ThrowsAsync<EndOfStreamException>(reader.DecodeAsync(System.Text.Encoding.UTF8, LengthFormat.LittleEndian, chars.Allocator, TestToken).AsTask);

        Equal(0, bytes.MaxLength);
        Equal(0, chars.MaxLength);

        // the allocator is also used for the internal buffer of the reader
        var internalBuffer = new AllocationTracker<byte>();
        using (var reader = new FileReader(handle) { Allocator = internalBuffer.Allocator })
            await ThrowsAsync<EndOfStreamException>(reader.ParseAsync<int>(LengthFormat.LittleEndian, token: TestToken).AsTask);

        True(internalBuffer.MaxLength <= 64 * 1024);
    }
}