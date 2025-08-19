using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace DotNext.IO.Pipelines;

using Buffers;

public sealed class PipeExtensionsTests : Test
{
    [Fact]
    public static async Task EncodeDecodeIntegers()
    {
        static async void WriteValuesAsync(PipeWriter writer)
        {
            writer.WriteLittleEndian(42L);
            writer.WriteLittleEndian(43UL);
            writer.WriteLittleEndian(44);
            writer.WriteBigEndian(45U);
            writer.WriteBigEndian<short>(46);
            writer.WriteBigEndian<ushort>(47);
            await writer.FlushAsync();
            await writer.CompleteAsync();
        }

        var pipe = new Pipe();
        WriteValuesAsync(pipe.Writer);
        Equal(42L, await pipe.Reader.ReadLittleEndianAsync<long>());
        Equal(43UL, await pipe.Reader.ReadLittleEndianAsync<ulong>());
        Equal(44, await pipe.Reader.ReadLittleEndianAsync<int>());
        Equal(45U, await pipe.Reader.ReadBigEndianAsync<uint>());
        Equal(46, await pipe.Reader.ReadBigEndianAsync<short>());
        Equal(47, await pipe.Reader.ReadBigEndianAsync<ushort>());
    }

    [Fact]
    public static async Task EncodeDecodeMemory()
    {
        static async void WriteValueAsync(Memory<byte> memory, PipeWriter writer)
        {
            await writer.WriteAsync(memory);
            await writer.CompleteAsync();
        }

        var pipe = new Pipe();
        WriteValueAsync(new byte[] { 1, 5, 8, 9, 10 }, pipe.Writer);
        var portion1 = new byte[3];
        var portion2 = new byte[2];
        await pipe.Reader.ReadExactlyAsync(portion1);
        await pipe.Reader.ReadExactlyAsync(portion2);
        Equal(1, portion1[0]);
        Equal(5, portion1[1]);
        Equal(8, portion1[2]);
        Equal(9, portion2[0]);
        Equal(10, portion2[1]);
    }

    [Fact]
    public static async Task EndOfMemory()
    {
        static async void WriteValueAsync(Memory<byte> memory, PipeWriter writer)
        {
            await writer.WriteAsync(memory);
            await writer.CompleteAsync();
        }

        var pipe = new Pipe();
        WriteValueAsync(new byte[] { 1, 5, 8, 9, 10 }, pipe.Writer);
        Memory<byte> result = new byte[124];
        await ThrowsAsync<EndOfStreamException>(() => pipe.Reader.ReadExactlyAsync(result).AsTask());
    }

    [Fact]
    public static async Task EncodeDecodeMemory2()
    {
        static async void WriteValueAsync(Memory<byte> memory, PipeWriter writer)
        {
            await writer.WriteAsync(memory);
            await writer.CompleteAsync();
        }

        var pipe = new Pipe();
        WriteValueAsync(new byte[] { 1, 5, 8, 9, 10 }, pipe.Writer);
        var portion1 = new byte[3];
        var portion2 = new byte[2];
        Equal(3, await pipe.Reader.ReadAsync(portion1));
        Equal(2, await pipe.Reader.ReadAsync(portion2));
        Equal(1, portion1[0]);
        Equal(5, portion1[1]);
        Equal(8, portion1[2]);
        Equal(9, portion2[0]);
        Equal(10, portion2[1]);
    }

    [Fact]
    public static async Task EndOfMemory2()
    {
        static async void WriteValueAsync(Memory<byte> memory, PipeWriter writer)
        {
            await writer.WriteAsync(memory);
            await writer.CompleteAsync();
        }

        var pipe = new Pipe();
        WriteValueAsync(new byte[] { 1, 5, 8, 9, 10 }, pipe.Writer);
        Memory<byte> result = new byte[124];
        Equal(5, await pipe.Reader.ReadAsync(result));
    }

    [Fact]
    public static async Task EncodeDecodeValue2()
    {
        static async void WriteValueAsync(PipeWriter writer)
        {
            writer.WriteLittleEndian(20L);
            writer.WriteLittleEndian(0L);
            await writer.FlushAsync();
            await writer.CompleteAsync();
        }

        var pipe = new Pipe();
        WriteValueAsync(pipe.Writer);
        Equal(20, await pipe.Reader.ReadLittleEndianAsync<Int128>());
    }

    [Fact]
    public static async Task EndOfStream()
    {
        static async void WriteValueAsync(PipeWriter writer)
        {
            writer.WriteLittleEndian(0L);
            await writer.FlushAsync();
            await writer.CompleteAsync();
        }

        var pipe = new Pipe();
        WriteValueAsync(pipe.Writer);
        await ThrowsAsync<EndOfStreamException>(pipe.Reader.ReadLittleEndianAsync<Int128>().AsTask);
    }

    private static async Task EncodeDecodeStringAsync(Encoding encoding, string value, LengthFormat lengthEnc)
    {
        var pipe = new Pipe();
        pipe.Writer.Encode(value.AsSpan(), encoding, lengthEnc);
        await pipe.Writer.FlushAsync();

        using var result = await pipe.Reader.DecodeAsync(encoding, lengthEnc);
        Equal(value, result.ToString());
    }

    [Theory]
    [InlineData(LengthFormat.Compressed)]
    [InlineData(LengthFormat.LittleEndian)]
    [InlineData(LengthFormat.BigEndian)]
    public static async Task EncodeDecodeString(LengthFormat lengthEnc)
    {
        const string testString = "abc^$&@^$&@)(_+~";
        await EncodeDecodeStringAsync(Encoding.UTF8, testString, lengthEnc);
        await EncodeDecodeStringAsync(Encoding.Unicode, testString, lengthEnc);
        await EncodeDecodeStringAsync(Encoding.UTF32, testString, lengthEnc);
        await EncodeDecodeStringAsync(Encoding.ASCII, testString, lengthEnc);
    }

    [Fact]
    public static async Task CopyToBuffer()
    {
        var pipe = new Pipe();
        await pipe.Writer.WriteAsync(new byte[] { 10, 20, 30 });
        await pipe.Writer.CompleteAsync();
        var buffer = new ArrayBufferWriter<byte>();
        await pipe.Reader.CopyToAsync<BufferConsumer<byte>>(new(buffer));
        Equal([10, 20, 30], buffer.WrittenMemory.ToArray());
    }

    [Fact]
    public static async Task ReadBlockUsingCallback()
    {
        static async void WriteValuesAsync(PipeWriter writer)
        {
            writer.WriteLittleEndian(42L);
            writer.WriteLittleEndian(43UL);
            writer.WriteLittleEndian(44);
            writer.WriteLittleEndian(45U);
            writer.WriteLittleEndian<short>(46);
            writer.WriteLittleEndian<ushort>(47);
            await writer.FlushAsync();
            await writer.CompleteAsync();
        }

        var pipe = new Pipe();
        WriteValuesAsync(pipe.Writer);
        var buffer = new ArrayBufferWriter<byte>();
        await pipe.Reader.CopyToAsync<BufferConsumer<byte>>(new(buffer));

        var reader = IAsyncBinaryReader.Create(buffer.WrittenMemory);
        Equal(42L, reader.ReadLittleEndian<long>());
        Equal(43UL, reader.ReadLittleEndian<ulong>());
        Equal(44, reader.ReadLittleEndian<int>());
        Equal(45U, reader.ReadLittleEndian<uint>());
        Equal(46, reader.ReadLittleEndian<short>());
        Equal(47, reader.ReadLittleEndian<ushort>());
    }

    [Fact]
    public static async Task ReadToEndUsingAsyncCallback()
    {
        static async void WriteValuesAsync(PipeWriter writer)
        {
            writer.WriteLittleEndian(42L);
            writer.WriteLittleEndian(43UL);
            writer.WriteLittleEndian(44);
            writer.WriteLittleEndian(45U);
            writer.WriteLittleEndian<short>(46);
            writer.WriteLittleEndian<ushort>(47);
            await writer.FlushAsync();
            await writer.CompleteAsync();
        }

        var pipe = new Pipe();
        WriteValuesAsync(pipe.Writer);
        var buffer = new ArrayBufferWriter<byte>();
        await pipe.Reader.CopyToAsync<BufferConsumer<byte>>(new(buffer));
        Equal(28, buffer.WrittenCount);
        var reader = IAsyncBinaryReader.Create(buffer.WrittenMemory);
        Equal(42L, reader.ReadLittleEndian<long>());
        Equal(43UL, reader.ReadLittleEndian<ulong>());
        Equal(44, reader.ReadLittleEndian<int>());
        Equal(45U, reader.ReadLittleEndian<uint>());
        Equal(46, reader.ReadLittleEndian<short>());
        Equal(47, reader.ReadLittleEndian<ushort>());
    }

    [Fact]
    public static async Task ReadToEndUsingCallback()
    {
        static async void WriteValuesAsync(PipeWriter writer)
        {
            writer.WriteLittleEndian(42L);
            writer.WriteLittleEndian(43UL);
            writer.WriteLittleEndian(44);
            writer.WriteLittleEndian(45U);
            writer.WriteLittleEndian<short>(46);
            writer.WriteLittleEndian<ushort>(47);
            await writer.FlushAsync();
            await writer.CompleteAsync();
        }

        var pipe = new Pipe();
        WriteValuesAsync(pipe.Writer);
        var buffer = new ArrayBufferWriter<byte>();
        await pipe.Reader.CopyToAsync<BufferConsumer<byte>>(new(buffer));
        Equal(28, buffer.WrittenCount);
        var reader = IAsyncBinaryReader.Create(buffer.WrittenMemory);
        Equal(42L, reader.ReadLittleEndian<long>());
        Equal(43UL, reader.ReadLittleEndian<ulong>());
        Equal(44, reader.ReadLittleEndian<int>());
        Equal(45U, reader.ReadLittleEndian<uint>());
        Equal(46, reader.ReadLittleEndian<short>());
        Equal(47, reader.ReadLittleEndian<ushort>());
    }

    [Fact]
    public static void ReadBlockSynchronously()
    {
        var pipe = new Pipe();
        pipe.Writer.Write(new byte[] { 10, 20, 30 });
        False(pipe.Reader.TryReadExactly(10L, out var result));
        True(result.Buffer.IsEmpty);
        False(result.IsCanceled);
        False(result.IsCompleted);

        pipe.Writer.Complete();
        True(pipe.Reader.TryReadExactly(3L, out result));
        True(result.IsCompleted);
        False(result.Buffer.IsEmpty);
        Equal(3L, result.Buffer.Length);
        Equal(10, result.Buffer.FirstSpan[0]);
        Equal(20, result.Buffer.FirstSpan[1]);
        Equal(30, result.Buffer.FirstSpan[2]);
    }

    [Fact]
    public static async Task ReadPortionAsync()
    {
        var portion1 = RandomBytes(64);
        var portion2 = RandomBytes(64);

        var reader = PipeReader.Create(Memory.Concat(new ReadOnlyMemory<byte>(portion1), portion2));

        await using (var enumerator = reader.ReadAllAsync().GetAsyncEnumerator())
        {
            True(await enumerator.MoveNextAsync());
            Equal(portion1, enumerator.Current.ToArray());
            True(await enumerator.MoveNextAsync());
        }

        await using (var enumerator = reader.ReadAllAsync().GetAsyncEnumerator())
        {
            True(await enumerator.MoveNextAsync());
            Equal(portion2, enumerator.Current.ToArray());
            False(await enumerator.MoveNextAsync());
        }
    }

    [Fact]
    public static async Task ReadBlockExactlyAsync()
    {
        var bytes = RandomBytes(128);

        var reader = PipeReader.Create(new(bytes));

        using var destination = new MemoryStream();
        await foreach (var chunk in reader.ReadExactlyAsync(64L))
        {
            await destination.WriteAsync(chunk);
        }

        Equal(new ReadOnlySpan<byte>(bytes, 0, 64), destination.ToArray());
    }

    [Fact]
    public static void ReadEmptyBlockAsync()
    {
        var reader = PipeReader.Create(ReadOnlySequence<byte>.Empty);

        Empty(reader.ReadExactlyAsync(0L));
    }

    [Fact]
    public static async Task ReadInvalidSizedBlockAsync()
    {
        var reader = PipeReader.Create(ReadOnlySequence<byte>.Empty);

        await using var enumerator = reader.ReadExactlyAsync(-1L).GetAsyncEnumerator();
        await ThrowsAsync<ArgumentOutOfRangeException>(enumerator.MoveNextAsync().AsTask);
    }

    [Fact]
    public static async Task DecodeNullTerminatedStringAsync()
    {
        var pipe = new Pipe();
        pipe.Writer.Write("Привет, мир!"u8);
        pipe.Writer.Write(stackalloc byte[] { 0, 0 });
        pipe.Writer.Complete();

        var writer = new ArrayBufferWriter<char>();
        await pipe.Reader.ReadUtf8Async(writer);
        Equal("Привет, мир!", writer.WrittenSpan.ToString());
    }

    [Fact]
    public static async Task WriteLargeData()
    {
        var pipe = new Pipe();
        var expectedData = RandomBytes(1024 * 1024);
        
        await Task.WhenAll(WriteAsync(), ReadAsync());

        async Task WriteAsync()
        {
            await pipe.Writer.WriteAsync(expectedData);
            await pipe.Writer.CompleteAsync();
        }

        async Task ReadAsync()
        {
            var offset = 0;
            await foreach (var block in pipe.Reader.ReadAllAsync())
            {
                Equal(expectedData.AsSpan(offset, block.Length), block.Span);
                offset += block.Length;
            }
        }
    }
}