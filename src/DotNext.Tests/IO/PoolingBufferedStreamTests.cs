using DotNext.Buffers;

namespace DotNext.IO;

public sealed class PoolingBufferedStreamTests : Test
{
    [Fact]
    public static void SimpleRead()
    {
        const int bufferSize = 4096;
        var expected = RandomBytes(bufferSize);
        using var stream = new MemoryStream(expected.Length);
        stream.Write(expected);
        stream.Seek(0L, SeekOrigin.Begin);

        using var bufferedStream = new PoolingBufferedStream(stream, leaveOpen: true) { MaxBufferSize = bufferSize };
        Same(stream, bufferedStream.BaseStream);
        False(bufferedStream.HasBufferedDataToRead);
        False(bufferedStream.HasBufferedDataToWrite);
        Equal(stream.Position, bufferedStream.Position);

        Equal(expected[0], bufferedStream.ReadByte());
        True(bufferedStream.HasBufferedDataToRead);
        False(bufferedStream.HasBufferedDataToWrite);
        Equal(bufferSize, stream.Position);
        Equal(1L, bufferedStream.Position);

        var actual = new byte[bufferSize - 1];
        Equal(actual.Length, bufferedStream.Read(actual));
        Equal(stream.Position, bufferedStream.Position);
        Equal(expected.AsSpan(1), actual.AsSpan());
        
        Equal(0, bufferedStream.Read(actual));
        Equal(-1, bufferedStream.ReadByte());
        False(bufferedStream.HasBufferedDataToRead);
        False(bufferedStream.HasBufferedDataToWrite);
    }
    
    [Fact]
    public static async Task SimpleReadAsync()
    {
        const int bufferSize = 4096;
        var expected = RandomBytes(bufferSize);
        await using var stream = new MemoryStream(expected.Length);
        stream.Write(expected);
        stream.Seek(0L, SeekOrigin.Begin);

        await using var bufferedStream = new PoolingBufferedStream(stream, leaveOpen: true) { MaxBufferSize = bufferSize };
        False(bufferedStream.HasBufferedDataToRead);
        False(bufferedStream.HasBufferedDataToWrite);
        Equal(stream.Position, bufferedStream.Position);

        Equal(expected[0], bufferedStream.ReadByte());
        True(bufferedStream.HasBufferedDataToRead);
        False(bufferedStream.HasBufferedDataToWrite);
        Equal(bufferSize, stream.Position);
        Equal(1L, bufferedStream.Position);

        var actual = new byte[bufferSize - 1];
        Equal(actual.Length, await bufferedStream.ReadAsync(actual));
        Equal(stream.Position, bufferedStream.Position);
        Equal(expected.AsSpan(1), actual.AsSpan());
        
        Equal(0, await bufferedStream.ReadAsync(actual));
        Equal(-1, bufferedStream.ReadByte());
        False(bufferedStream.HasBufferedDataToRead);
        False(bufferedStream.HasBufferedDataToWrite);
    }

    [Fact]
    public static void BufferizeAndAdvancePosition()
    {
        const int bufferSize = 4096;
        var expected = RandomBytes(bufferSize);
        using var stream = new MemoryStream(expected.Length);
        stream.Write(expected);
        stream.Seek(0L, SeekOrigin.Begin);

        using var bufferedStream = new PoolingBufferedStream(stream, leaveOpen: true) { MaxBufferSize = bufferSize };
        True(bufferedStream.Read());

        bufferedStream.Position = bufferSize / 2;
        True(bufferedStream.HasBufferedDataToRead);

        var actual = new byte[bufferSize / 2];
        Equal(actual.Length, bufferedStream.Read(actual, 0, actual.Length));
        Equal(stream.Position, bufferedStream.Position);

        Equal(expected.AsSpan(bufferSize / 2), actual.AsSpan());
    }
    
    [Fact]
    public static async Task BufferizeAndAdvancePositionAsync()
    {
        const int bufferSize = 4096;
        var expected = RandomBytes(bufferSize);
        using var stream = new MemoryStream(expected.Length);
        stream.Write(expected);
        stream.Seek(0L, SeekOrigin.Begin);

        await using var bufferedStream = new PoolingBufferedStream(stream, leaveOpen: true) { MaxBufferSize = bufferSize };
        True(await bufferedStream.ReadAsync());

        bufferedStream.Position = bufferSize / 2;
        Equal(bufferSize, stream.Position);
        True(bufferedStream.HasBufferedDataToRead);

        var actual = new byte[bufferSize / 2];
        Equal(actual.Length, await bufferedStream.ReadAsync(actual, 0, actual.Length));
        Equal(stream.Position, bufferedStream.Position);

        Equal(expected.AsSpan(bufferSize / 2), actual.AsSpan());
    }

    [Fact]
    public static void DrainBuffer()
    {
        const int bufferSize = 4096;
        using var bufferedStream = new PoolingBufferedStream(new MemoryStream(bufferSize)) { MaxBufferSize = bufferSize };

        var expected = RandomBytes(bufferSize);
        bufferedStream.Write(expected);
        True(bufferedStream.HasBufferedDataToWrite);
        False(bufferedStream.HasBufferedDataToRead);

        Equal(0L, bufferedStream.BaseStream.Position);
        Equal(bufferSize, bufferedStream.Length);

        bufferedStream.Write();
        Equal(bufferSize, bufferedStream.BaseStream.Position);
        Equal(bufferedStream.BaseStream.Length, bufferedStream.Length);
    }
    
    [Fact]
    public static async Task DrainBufferAsync()
    {
        const int bufferSize = 4096;
        await using var bufferedStream = new PoolingBufferedStream(new MemoryStream(bufferSize)) { MaxBufferSize = bufferSize };

        var expected = RandomBytes(bufferSize);
        await bufferedStream.WriteAsync(expected);
        True(bufferedStream.HasBufferedDataToWrite);
        False(bufferedStream.HasBufferedDataToRead);

        Equal(0L, bufferedStream.BaseStream.Position);
        Equal(bufferSize, bufferedStream.Length);

        await bufferedStream.WriteAsync();
        Equal(bufferSize, bufferedStream.BaseStream.Position);
        Equal(bufferedStream.BaseStream.Length, bufferedStream.Length);
    }

    [Fact]
    public static void CheckProperties()
    {
        using var stream = new MemoryStream();
        using var bufferedStream = new PoolingBufferedStream(stream, leaveOpen: true);

        Equal(stream.CanRead, bufferedStream.CanRead);
        Equal(stream.CanWrite, bufferedStream.CanWrite);
        Equal(stream.CanSeek, bufferedStream.CanSeek);
        Equal(stream.CanTimeout, bufferedStream.CanTimeout);

        Throws<InvalidOperationException>(() => stream.ReadTimeout);
        Throws<InvalidOperationException>(() => stream.ReadTimeout = 10);
        
        Throws<InvalidOperationException>(() => stream.WriteTimeout);
        Throws<InvalidOperationException>(() => stream.WriteTimeout = 10);
    }

    [Fact]
    public static async Task BufferedWriter()
    {
        const int bufferSize = 4096;
        await using var bufferedStream = new PoolingBufferedStream(new MemoryStream(bufferSize)) { MaxBufferSize = bufferSize };

        var expected = RandomBytes(bufferSize);
        IBufferedWriter writer = bufferedStream;
        expected.CopyTo(writer.Buffer);
        writer.Produce(expected.Length);
        True(bufferedStream.HasBufferedDataToWrite);
        False(bufferedStream.HasBufferedDataToRead);
        await writer.WriteAsync();
        
        False(bufferedStream.HasBufferedDataToWrite);
        False(bufferedStream.HasBufferedDataToRead);
    }
    
    [Fact]
    public static async Task BufferedReader()
    {
        const int bufferSize = 4096;
        await using var bufferedStream = new PoolingBufferedStream(new MemoryStream(bufferSize)) { MaxBufferSize = bufferSize };

        var expected = RandomBytes(bufferSize);
        await bufferedStream.WriteAsync(expected);
        await bufferedStream.WriteAsync();
        bufferedStream.Position = 0L;

        False(bufferedStream.HasBufferedDataToRead);
        IBufferedReader reader = bufferedStream;
        await reader.ReadAsync();
        True(bufferedStream.HasBufferedDataToRead);
        Equal(expected, reader.Buffer);
    }
    
    [Fact]
    public static void InvalidReaderWriterStates()
    {
        const int bufferSize = 4096;
        using var bufferedStream = new PoolingBufferedStream(new MemoryStream(bufferSize)) { MaxBufferSize = bufferSize };
        bufferedStream.WriteByte(42);
        bufferedStream.WriteByte(43);
        Throws<InvalidOperationException>(() => bufferedStream.As<IBufferedReader>().Buffer);
        Throws<InvalidOperationException>(() => bufferedStream.As<IBufferedReader>().Consume(1));
        
        bufferedStream.Flush();
        bufferedStream.Position = 0;
        bufferedStream.Read();
        bufferedStream.As<IBufferedReader>().Consume(1);
        Equal<byte>([43], bufferedStream.As<IBufferedReader>().Buffer.Span);

        Throws<InvalidOperationException>(() => bufferedStream.As<IBufferedWriter>().Buffer);
        Throws<InvalidOperationException>(() => bufferedStream.As<IBufferedWriter>().Produce(1));
    }

    [Fact]
    public static void StreamLength()
    {
        const int bufferSize = 4096;
        using var bufferedStream = new PoolingBufferedStream(new MemoryStream(bufferSize)) { MaxBufferSize = bufferSize };
        Equal(0L, bufferedStream.Length);
        
        bufferedStream.WriteByte(42);
        bufferedStream.WriteByte(43);
        Equal(2L, bufferedStream.Length);
    }

    [Fact]
    public static void CopyStream()
    {
        const int bufferSize = 4096;
        using var bufferedStream = new PoolingBufferedStream(new MemoryStream(bufferSize)) { MaxBufferSize = bufferSize };

        var expected = RandomBytes(bufferSize);
        bufferedStream.Write(expected);
        bufferedStream.Flush();
        bufferedStream.Position = 0L;

        using var destination = new MemoryStream(bufferSize);
        bufferedStream.CopyTo(destination);

        Equal(expected, destination.GetBuffer());
    }
    
    [Fact]
    public static void CopyStream2()
    {
        const int bufferSize = 4096;
        using var bufferedStream = new PoolingBufferedStream(new MemoryStream(bufferSize)) { MaxBufferSize = bufferSize };

        var expected = RandomBytes(bufferSize);
        bufferedStream.Write(expected);
        bufferedStream.Flush();
        bufferedStream.Position = 0L;
        True(bufferedStream.Read());

        using var destination = new MemoryStream(bufferSize);
        bufferedStream.CopyTo(destination);

        Equal(expected, destination.GetBuffer());
    }
    
    [Fact]
    public static async Task CopyStreamAsync()
    {
        const int bufferSize = 4096;
        await using var bufferedStream = new PoolingBufferedStream(new MemoryStream(bufferSize)) { MaxBufferSize = bufferSize };

        var expected = RandomBytes(bufferSize);
        await bufferedStream.WriteAsync(expected);
        await bufferedStream.FlushAsync();
        bufferedStream.Position = 0L;

        await using var destination = new MemoryStream(bufferSize);
        await bufferedStream.CopyToAsync(destination);

        Equal(expected, destination.GetBuffer());
    }
    
    [Fact]
    public static async Task CopyStream2Async()
    {
        const int bufferSize = 4096;
        await using var bufferedStream = new PoolingBufferedStream(new MemoryStream(bufferSize)) { MaxBufferSize = bufferSize };

        var expected = RandomBytes(bufferSize);
        await bufferedStream.WriteAsync(expected);
        await bufferedStream.FlushAsync();
        bufferedStream.Position = 0L;
        True(await bufferedStream.ReadAsync());

        await using var destination = new MemoryStream(bufferSize);
        await bufferedStream.CopyToAsync(destination);

        Equal(expected, destination.GetBuffer());
    }

    [Fact]
    public static void SetLength()
    {
        const int bufferSize = 4096;
        using var stream = new MemoryStream(bufferSize);
        using var bufferedStream = new PoolingBufferedStream(stream, leaveOpen: false)
        {
            MaxBufferSize = bufferSize,
            Allocator = Memory.GetArrayAllocator<byte>(),
        };

        var expected = RandomBytes(bufferSize);
        bufferedStream.Write(expected);
        
        bufferedStream.SetLength(bufferSize);
        Equal(expected, stream.GetBuffer());
    }
    
    [Fact]
    public static void ResetBuffer()
    {
        const int bufferSize = 4096;
        using var bufferedStream = new PoolingBufferedStream(new MemoryStream(bufferSize), leaveOpen: false)
        {
            MaxBufferSize = bufferSize,
            Allocator = Memory.GetArrayAllocator<byte>(),
        };

        var expected = RandomBytes(bufferSize);
        bufferedStream.Write(expected);
        True(bufferedStream.HasBufferedDataToWrite);
        
        bufferedStream.Reset();
        False(bufferedStream.HasBufferedDataToWrite);
    }

    [Fact]
    public static void ReadFromFile()
    {
        var expected = RandomBytes(4096);
        using var handle = File.OpenHandle(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), FileMode.CreateNew, FileAccess.ReadWrite,
            options: FileOptions.DeleteOnClose);
        RandomAccess.Write(handle, expected, fileOffset: 0L);
        RandomAccess.FlushToDisk(handle);

        using var bufferedStream = new PoolingBufferedStream(handle.AsUnbufferedStream(FileAccess.Read)) { MaxBufferSize = 128 };
        var actual = new byte[expected.Length];
        bufferedStream.ReadExactly(actual);

        Equal(expected, actual);
    }
    
    [Fact]
    public static async Task ReadFromFileAsync()
    {
        var expected = RandomBytes(4096);
        using var handle = File.OpenHandle(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), FileMode.CreateNew, FileAccess.ReadWrite,
            options: FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        await RandomAccess.WriteAsync(handle, expected, fileOffset: 0L);
        RandomAccess.FlushToDisk(handle);

        await using var bufferedStream = new PoolingBufferedStream(handle.AsUnbufferedStream(FileAccess.Read)) { MaxBufferSize = 128 };
        var actual = new byte[expected.Length];
        await bufferedStream.ReadExactlyAsync(actual);

        Equal(expected, actual);
    }

    [Fact]
    public static void OverwriteStream()
    {
        using var buffered = new PoolingBufferedStream(new MemoryStream(), leaveOpen: false);
        Equal(0L, buffered.Position);
        
        buffered.Write("text"u8);
        Equal(4L, buffered.Position);
        
        buffered.Position = 0;
        Equal(0L, buffered.Position);
        
        buffered.Write("text"u8);
        Equal(4L, buffered.Position);
    }

    [Fact]
    public static async Task RegressionIssue256Async()
    {
        const int dataSize = 128 + 3105 + 66 + 3111 + 66 + 3105 + 66 + 2513 + 128;
        ReadOnlyMemory<byte> expected = RandomBytes(dataSize);
        await using var ms = new MemoryStream();

        await using (var buffered = new PoolingBufferedStream(ms, leaveOpen: true) { MaxBufferSize = 8192 })
        {
            await buffered.WriteAsync(expected);
            await buffered.FlushAsync();
        }

        ms.Position = 0;
        await using (var reader = new PoolingBufferedStream(ms, leaveOpen: true) { MaxBufferSize = 4096 })
        {
            Memory<byte> buffer = new byte[dataSize];
            await reader.ReadExactlyAsync(buffer.Slice(0, 3175));
            reader.Position = 3303;
            await reader.ReadExactlyAsync(buffer.Slice(0, 3107));
            Equal(expected.Slice(3303, 3107), buffer.Slice(0, 3107));
        }
    }
    
    [Fact]
    public static void RegressionIssue256()
    {
        const int dataSize = 128 + 3105 + 66 + 3111 + 66 + 3105 + 66 + 2513 + 128;
        ReadOnlySpan<byte> expected = RandomBytes(dataSize);
        using var ms = new MemoryStream();

        using (var buffered = new PoolingBufferedStream(ms, leaveOpen: true) { MaxBufferSize = 8192 })
        {
            buffered.Write(expected);
            buffered.Flush();
        }

        ms.Position = 0;
        using (var reader = new PoolingBufferedStream(ms, leaveOpen: true) { MaxBufferSize = 4096 })
        {
            Span<byte> buffer = new byte[dataSize];
            reader.ReadExactly(buffer.Slice(0, 3175));
            reader.Position = 3303;
            reader.ReadExactly(buffer.Slice(0, 3107));
            Equal(expected.Slice(3303, 3107), buffer.Slice(0, 3107));
        }
    }

    [Fact]
    public static void RepeatableReads()
    {
        var bytes = RandomBytes(128);
        using var reader = new PoolingBufferedStream(new MemoryStream(bytes)) { MaxBufferSize = 256 };
        True(reader.Read());
        False(reader.Read());
        True(reader.HasBufferedDataToRead);

        Equal(bytes, reader.As<IBufferedReader>().Buffer);
    }
    
    [Fact]
    public static void ReadEmpty()
    {
        var bytes = RandomBytes(128);
        using var reader = new PoolingBufferedStream(new MemoryStream(bytes)) { MaxBufferSize = 256 };
        True(reader.Read());
        reader.ReadExactly(new byte[bytes.Length]);
        False(reader.Read());
        False(reader.HasBufferedDataToRead);
    }
}