using System.Buffers;
using System.Runtime.CompilerServices;

namespace DotNext.IO;

using Buffers;
using Runtime.CompilerServices;

public sealed class StreamSourceTests : Test
{
    private static readonly byte[] data = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120];

    public static TheoryData<ReadOnlySequence<byte>> TestBuffers() => new()
    {
        new ReadOnlySequence<byte>(data),
        ToReadOnlySequence<byte>(data, 2),
        ToReadOnlySequence<byte>(data, 3),
    };

    [Theory]
    [MemberData(nameof(TestBuffers))]
    public static void CopyToStream(ReadOnlySequence<byte> sequence)
    {
        using var dest = new MemoryStream();
        using var src = sequence.AsStream();
        Equal(0L, src.Position);
        Equal(sequence.Length, src.Length);

        src.CopyTo(dest);
        dest.Position = 0;
        Equal(data, dest.ToArray());
    }

    [Theory]
    [MemberData(nameof(TestBuffers))]
    public static void CopySparseMemory(ReadOnlySequence<byte> sequence)
    {
        using var dest = new MemoryStream();
        using var buffer = new SparseBufferWriter<byte>();
        buffer.Write(in sequence, copyMemory: false);
        using var src = buffer.AsStream(true);
        Equal(0L, src.Position);

        src.CopyTo(dest, bufferSize: 32);
        Equal(dest.Position, src.Position);

        dest.Position = 0;
        Equal(data, dest.ToArray());
    }

    [Fact]
    public static void SparseMemoryStreamMembers()
    {
        using var buffer = new SparseBufferWriter<byte>();
        buffer.Write(ToReadOnlySequence<byte>(data, 2), copyMemory: false);
        using var src = buffer.AsStream(true);

        False(src.CanSeek);
        Throws<NotSupportedException>(() => src.Position = 0L);
        Throws<NotSupportedException>(() => src.SetLength(10L));
        Throws<NotSupportedException>(() => src.Seek(0L, SeekOrigin.Begin));
    }

    [Fact]
    public static void EmptyCopyTo()
    {
        using var dest = new MemoryStream();
        using var src = ReadOnlySequence<byte>.Empty.AsStream();
        Same(Stream.Null, src);
        Equal(0L, src.Length);

        src.CopyTo(dest);
        Equal(0L, dest.Length);
    }

    [Fact]
    public static void SeekAndCopy()
    {
        using var dest = new MemoryStream();
        using var src = ToReadOnlySequence<byte>(data, 5).AsStream();
        NotEqual(0L, src.Length);
        src.Position = data.Length;

        src.CopyTo(dest);
        Equal(0L, dest.Length);
    }

    [Theory]
    [MemberData(nameof(TestBuffers))]
    public static async Task CopyToStreamAsync(ReadOnlySequence<byte> sequence)
    {
        using var dest = new MemoryStream();
        using var src = sequence.AsStream();

        await src.CopyToAsync(dest);
        dest.Position = 0;
        Equal(data, dest.ToArray());
    }

    [Theory]
    [MemberData(nameof(TestBuffers))]
    public static async Task CopySparseMemoryAsync(ReadOnlySequence<byte> sequence)
    {
        using var dest = new MemoryStream();
        using var buffer = new SparseBufferWriter<byte>();
        buffer.Write(in sequence);
        using var src = buffer.AsStream(true);
        await src.CopyToAsync(dest);
        dest.Position = 0;
        Equal(data, dest.ToArray());
    }

    [Fact]
    public static void CopyAfterReuse()
    {
        var dest = new MemoryStream();
        using var src = ToReadOnlySequence<byte>(data, 5).AsStream();

        src.CopyTo(dest);
        Equal(data, dest.ToArray());
        Equal(data.Length, src.Length);

        dest.Dispose();
        dest = new MemoryStream();
        src.Position = 0L;
        src.CopyTo(dest);
        Equal(data, dest.ToArray());
        Equal(data.Length, src.Length);
    }

    [Fact]
    public static void SeekFromEnd()
    {
        using var dest = new MemoryStream();
        using var src = ToReadOnlySequence<byte>(data, 5).AsStream();
        Equal(data.Length - 1, src.Seek(-1L, SeekOrigin.End));
        src.CopyTo(dest);
        Equal(1L, dest.Length);
        Equal(data[^1], dest.ToArray()[0]);
    }

    [Fact]
    public static void SeekFromStart()
    {
        using var dest = new MemoryStream();
        using var src = ToReadOnlySequence<byte>(data, 5).AsStream();
        Equal(data.Length - 1, src.Seek(data.Length - 1, SeekOrigin.Begin));
        src.CopyTo(dest);
        Equal(1L, dest.Length);
        Equal(data[^1], dest.ToArray()[0]);
    }

    [Fact]
    public static void SeekFromCurrent()
    {
        using var dest = new MemoryStream();
        using var src = ToReadOnlySequence<byte>(data, 5).AsStream();
        src.Position = 1;
        Equal(data.Length - 1, src.Seek(data.Length - 2, SeekOrigin.Current));
        src.CopyTo(dest);
        Equal(1L, dest.Length);
        Equal(data[^1], dest.ToArray()[0]);
    }

    [Theory]
    [MemberData(nameof(TestBuffers))]
    public static void ReadSpan(ReadOnlySequence<byte> sequence)
    {
        using var src = sequence.AsStream();
        Span<byte> dest = new byte[data.Length];
        Equal(dest.Length, src.Read(dest));
        Equal(data, dest.ToArray());

        src.Position = sequence.Length - 1;
        Equal(1, src.Read(dest.Slice(0, 1)));
        Equal(data[^1], dest[0]);
    }

    [Theory]
    [MemberData(nameof(TestBuffers))]
    public static void ReadBlockFromSparseMemory(ReadOnlySequence<byte> sequence)
    {
        using var buffer = new SparseBufferWriter<byte>();
        buffer.Write(in sequence);
        using var src = buffer.AsStream(true);
        Span<byte> dest = new byte[data.Length];
        src.ReadExactly(dest);
        Equal(data, dest.ToArray());
    }

    [Theory]
    [MemberData(nameof(TestBuffers))]
    public static void ReadArray(ReadOnlySequence<byte> sequence)
    {
        using var src = sequence.AsStream();
        var dest = new byte[data.Length];
        Equal(dest.Length, src.Read(dest, 0, dest.Length));
        Equal(data, dest);

        src.Position = sequence.Length - 1;
        Equal(1, src.Read(dest, 0, 1));
        Equal(data[^1], dest[0]);
    }

    [Theory]
    [MemberData(nameof(TestBuffers))]
    public static async Task ReadMemory(ReadOnlySequence<byte> sequence)
    {
        using var src = sequence.AsStream();
        Memory<byte> dest = new byte[data.Length];
        Equal(dest.Length, await src.ReadAsync(dest));
        Equal(data, dest.ToArray());

        src.Position = sequence.Length - 1;
        Equal(1, await src.ReadAsync(dest.Slice(0, 1)));
        Equal(data[^1], dest.Span[0]);
    }

    [Theory]
    [MemberData(nameof(TestBuffers))]
    public static async Task ReadBlockFromSparseMemoryAsync(ReadOnlySequence<byte> sequence)
    {
        using var buffer = new SparseBufferWriter<byte>();
        buffer.Write(in sequence);
        using var src = buffer.AsStream(true);
        Equal(src.Length, sequence.Length);
        Equal(0L, src.Position);
        Memory<byte> dest = new byte[data.Length];
        await src.ReadExactlyAsync(dest);
        Equal(src.Length, src.Position);
        Equal(data, dest.ToArray());
    }

    [Theory]
    [MemberData(nameof(TestBuffers))]
    public static async Task ReadArrayAsync(ReadOnlySequence<byte> sequence)
    {
        using var src = sequence.AsStream();
        var dest = new byte[data.Length];
        Equal(dest.Length, await src.ReadAsync(dest, 0, dest.Length));
        Equal(data, dest);

        src.Position = sequence.Length - 1;
        Equal(1, await src.ReadAsync(dest, 0, 1));
        Equal(data[^1], dest[0]);
    }

    [Theory]
    [MemberData(nameof(TestBuffers))]
    public static void ReadSingleByte(ReadOnlySequence<byte> sequence)
    {
        using var src = sequence.AsStream();

        for (var i = 0; i < data.Length; i++)
            Equal(data[i], src.ReadByte());

        src.Seek(-1L, SeekOrigin.End);
        Equal(data[^1], src.ReadByte());
        Equal(-1, src.ReadByte());
    }

    [Theory]
    [MemberData(nameof(TestBuffers))]
    public static void ReadSingleByteFromSparseMemory(ReadOnlySequence<byte> sequence)
    {
        using var buffer = new SparseBufferWriter<byte>();
        buffer.Write(in sequence);

        var src = buffer.AsStream(true);
        for (var i = 0; i < data.Length; i++)
            Equal(data[i], src.ReadByte());
    }

    [Fact]
    public static void InvalidSeek()
    {
        using var src = ToReadOnlySequence<byte>(data, 6).AsStream();
        Throws<ArgumentOutOfRangeException>(() => src.Seek(500L, SeekOrigin.Begin));
        Throws<IOException>(() => src.Seek(-500L, SeekOrigin.End));
    }

    private sealed class CallbackChecker : TaskCompletionSource<bool>
    {
        internal void DoCallback(IAsyncResult ar) => SetResult(true);
    }

    [Fact]
    public static void ReadApm()
    {
        using var src = ToReadOnlySequence<byte>(data, 5).AsStream();
        var buffer = new byte[4];
        src.Position = 1;
        var ar = src.BeginRead(buffer, 0, 2, null, "state");
        True(ar.CompletedSynchronously);
        Equal("state", ar.AsyncState);
        True(ar.AsyncWaitHandle.WaitOne(DefaultTimeout));
        Equal(2, src.EndRead(ar));
        Equal(data[1], buffer[0]);
        Equal(data[2], buffer[1]);
        Equal(0, buffer[2]);
    }

    [Fact]
    public static async Task ReadApm2()
    {
        using var src = ToReadOnlySequence<byte>(data, 5).AsStream();
        var buffer = new byte[4];
        src.Position = 1;
        var checker = new CallbackChecker();
        var ar = src.BeginRead(buffer, 0, 2, checker.DoCallback, "state");
        True(ar.CompletedSynchronously);
        Equal("state", ar.AsyncState);
        True(await checker.Task);
        Equal(2, src.EndRead(ar));
        Equal(data[1], buffer[0]);
        Equal(data[2], buffer[1]);
        Equal(0, buffer[2]);
    }

    [Fact]
    public static void Truncation()
    {
        using var src = ToReadOnlySequence<byte>(data, 5).AsStream();
        src.Position = 1L;
        src.SetLength(data.Length - 2L);
        Equal(data.Length - 2L, src.Length);
        var buffer = new byte[3];
        Equal(3, src.Read(buffer));
        Equal(data[1], buffer[0]);
        Equal(data[2], buffer[1]);
        Equal(data[3], buffer[2]);

        src.Position = src.Length;
        src.SetLength(1L);
        buffer[0] = 0;
        Equal(0, src.Read(buffer));
    }

    [Fact]
    public static async Task WriteNotSupported()
    {
        using var src = new ReadOnlyMemory<byte>(data).AsStream();
        True(src.CanRead);
        True(src.CanSeek);
        False(src.CanWrite);
        False(src.CanTimeout);
        Throws<NotSupportedException>(() => src.Write(new byte[2], 0, 2));
        Throws<NotSupportedException>(() => src.Write(new byte[2]));
        Throws<NotSupportedException>(() => src.WriteByte(42));
        Throws<NotSupportedException>(() => src.BeginWrite(new byte[2], 0, 2, null, null));
        Throws<ArgumentException>(() => src.EndWrite(Task.CompletedTask));
        await ThrowsAsync<NotSupportedException>(src.WriteAsync(new ReadOnlyMemory<byte>()).AsTask);
        await ThrowsAsync<NotSupportedException>(() => src.WriteAsync(new byte[2], 0, 2));
    }

    [Fact]
    public static async Task WriteNotSupported2()
    {
        using var src = ToReadOnlySequence<byte>(data, 5).AsStream();
        True(src.CanRead);
        True(src.CanSeek);
        False(src.CanWrite);
        False(src.CanTimeout);
        Throws<NotSupportedException>(() => src.Write(new byte[2], 0, 2));
        Throws<NotSupportedException>(() => src.Write(new byte[2]));
        Throws<NotSupportedException>(() => src.WriteByte(42));
        Throws<NotSupportedException>(() => src.BeginWrite(new byte[2], 0, 2, null, null));
        Throws<InvalidOperationException>(() => src.EndWrite(Task.CompletedTask));
        await ThrowsAsync<NotSupportedException>(src.WriteAsync(new ReadOnlyMemory<byte>()).AsTask);
        await ThrowsAsync<NotSupportedException>(() => src.WriteAsync(new byte[2], 0, 2));
    }

    [Fact]
    public static async Task StressTest()
    {
        ReadOnlySequence<byte> sequence;
        var dict = new Dictionary<string, string>
            {
                {"Key1", "Value1"},
                {"Key2", "Value2"}
            };

        Memory<byte> buffer = new byte[16];
        using (var ms = new MemoryStream(1024))
        {
            await DictionarySerializer.SerializeAsync(dict, ms, buffer);
            ms.Position = 0L;
            sequence = ToReadOnlySequence<byte>(ms.ToArray(), 10);
        }

        using var stream = sequence.AsStream();
        Equal(dict, await DictionarySerializer.DeserializeAsync(stream, buffer));
    }

    private sealed class FlushCounter
    {
        internal volatile int Value;

        internal void Flush(IBufferWriter<byte> writer)
        {
            NotNull(writer);
            Interlocked.Increment(ref Value);
        }

        internal Task FlushAsync(IBufferWriter<byte> writer, CancellationToken token)
        {
            NotNull(writer);
            token.ThrowIfCancellationRequested();
            Interlocked.Increment(ref Value);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public static void BufferWriterToWritableStream()
    {
        var writer = new ArrayBufferWriter<byte>();
        var counter = new FlushCounter();
        using var stream = writer.AsStream(flush: counter.Flush);

        stream.Write(new byte[2] { 10, 20 });
        stream.Write(new byte[2] { 30, 40 }, 1, 1);
        stream.Flush();
        Equal(1, counter.Value);

        Equal(3, writer.WrittenCount);

        Equal(10, writer.WrittenSpan[0]);
        Equal(20, writer.WrittenSpan[1]);
        Equal(40, writer.WrittenSpan[2]);
        Equal(3, stream.Position);

        stream.WriteByte(50);
        Equal(4, stream.Position);
        Equal(4, writer.WrittenCount);
        Equal(50, writer.WrittenSpan[3]);
    }

    [Fact]
    public static async Task BufferWriterToWritableStreamAsync()
    {
        var writer = new ArrayBufferWriter<byte>();
        var counter = new FlushCounter();
        using var stream = writer.AsStream(flushAsync: counter.FlushAsync);

        await stream.WriteAsync(new byte[2] { 10, 20 });
        await stream.WriteAsync(new byte[2] { 30, 40 }, 1, 1);
        stream.Flush();
        Equal(1, counter.Value);
        await stream.FlushAsync();
        Equal(2, counter.Value);

        Equal(3, writer.WrittenCount);

        Equal(10, writer.WrittenSpan[0]);
        Equal(20, writer.WrittenSpan[1]);
        Equal(40, writer.WrittenSpan[2]);
        Equal(3, stream.Position);
    }

    [Fact]
    public static void BufferWriterToWritableStreamApm()
    {
        var writer = new ArrayBufferWriter<byte>();
        using var stream = writer.AsStream();
        var ar = stream.BeginWrite(new byte[2] { 30, 40 }, 1, 1, null, "state");
        Equal("state", ar.AsyncState);
        True(ar.AsyncWaitHandle.WaitOne(DefaultTimeout));
        stream.EndWrite(ar);
        Equal(1L, stream.Position);
        Equal(1, writer.WrittenCount);
        stream.Flush();
        Equal(40, writer.WrittenSpan[0]);
    }

    [Fact]
    public static async Task BufferWriterToWritableStreamApm2()
    {
        var writer = new ArrayBufferWriter<byte>();
        using var stream = writer.AsStream();
        var checker = new CallbackChecker();
        var ar = stream.BeginWrite(new byte[2] { 30, 40 }, 1, 1, checker.DoCallback, "state");
        Equal("state", ar.AsyncState);
        True(await checker.Task);
        stream.EndWrite(ar);
        Equal(1L, stream.Position);
        Equal(1L, stream.Length);
        Equal(1, writer.WrittenCount);
        await stream.FlushAsync();
        Equal(40, writer.WrittenSpan[0]);
    }

    [Fact]
    public static async Task BufferWriterStreamUnsupportedMethods()
    {
        var writer = new ArrayBufferWriter<byte>();
        using var stream = writer.AsStream();
        False(stream.CanRead);
        False(stream.CanSeek);
        True(stream.CanWrite);
        False(stream.CanTimeout);

        Throws<NotSupportedException>(() => stream.ReadByte());
        Throws<NotSupportedException>(() => stream.Read(new byte[2]));
        Throws<NotSupportedException>(() => stream.Read(new byte[2], 0, 2));
        Throws<NotSupportedException>(() => stream.Position = 0);
        Throws<NotSupportedException>(() => stream.SetLength(10L));
        Throws<NotSupportedException>(() => stream.Seek(-1L, SeekOrigin.End));
        Throws<NotSupportedException>(() => stream.BeginRead(new byte[2], 0, 2, null, null));
        Throws<InvalidOperationException>(() => stream.EndRead(Task.CompletedTask));
        await ThrowsAsync<NotSupportedException>(stream.ReadAsync(new byte[2]).AsTask);
        await ThrowsAsync<NotSupportedException>(() => stream.ReadAsync(new byte[2], 0, 2));
    }

    [Fact]
    public static void SpanActionToStream()
    {
        static void WriteToBuffer(ReadOnlySpan<byte> block, ArrayBufferWriter<byte> writer)
            => writer.Write(block);

        var writer = new ArrayBufferWriter<byte>();
        ReadOnlySpanAction<byte, ArrayBufferWriter<byte>> callback = WriteToBuffer;
        using var stream = callback.AsStream(writer);
        byte[] content = { 1, 2, 3 };
        stream.Write(content);
        stream.Flush();
        Equal(3, stream.Position);
        Equal(3, stream.Length);
        Equal(content, writer.WrittenMemory.ToArray());
    }

    [Fact]
    public static async Task SpanActionToStreamAsync()
    {
        static void WriteToBuffer(ReadOnlySpan<byte> block, ArrayBufferWriter<byte> writer)
            => writer.Write(block);

        var writer = new ArrayBufferWriter<byte>();
        ReadOnlySpanAction<byte, ArrayBufferWriter<byte>> callback = WriteToBuffer;
        using var stream = callback.AsStream(writer);
        byte[] content = { 1, 2, 3 };
        await stream.WriteAsync(content);
        await stream.FlushAsync();
        Equal(3, stream.Position);
        Equal(content, writer.WrittenMemory.ToArray());
    }

    [Fact]
    public static void MemoryFuncToStream()
    {
        static ValueTask WriteToBuffer(ReadOnlyMemory<byte> block, ArrayBufferWriter<byte> writer, CancellationToken token)
        {
            writer.Write(block.Span);
            return new ValueTask();
        }

        var writer = new ArrayBufferWriter<byte>();
        Func<ReadOnlyMemory<byte>, ArrayBufferWriter<byte>, CancellationToken, ValueTask> callback = WriteToBuffer;
        using var stream = callback.AsStream(writer);
        byte[] content = { 1, 2, 3 };
        stream.Write(content);
        stream.Flush();
        Equal(3, stream.Position);
        Equal(content, writer.WrittenMemory.ToArray());
    }

    [Fact]
    public static async Task MemoryFuncToStreamAsync()
    {
        static ValueTask WriteToBuffer(ReadOnlyMemory<byte> block, ArrayBufferWriter<byte> writer, CancellationToken token)
        {
            writer.Write(block.Span);
            return new ValueTask();
        }

        var writer = new ArrayBufferWriter<byte>();
        Func<ReadOnlyMemory<byte>, ArrayBufferWriter<byte>, CancellationToken, ValueTask> callback = WriteToBuffer;
        using var stream = callback.AsStream(writer);
        byte[] content = { 1, 2, 3 };
        await stream.WriteAsync(content);
        await stream.FlushAsync();
        Equal(3, stream.Position);
        Equal(content, writer.WrittenMemory.ToArray());
    }

    [Fact]
    public static async Task MemoryFuncToStreamApm()
    {
        static ValueTask WriteToBuffer(ReadOnlyMemory<byte> block, ArrayBufferWriter<byte> writer, CancellationToken token)
        {
            writer.Write(block.Span);
            return new ValueTask();
        }

        var writer = new ArrayBufferWriter<byte>();
        Func<ReadOnlyMemory<byte>, ArrayBufferWriter<byte>, CancellationToken, ValueTask> callback = WriteToBuffer;
        using var stream = callback.AsStream(writer);
        byte[] content = { 1, 2, 3 };
        var checker = new CallbackChecker();
        var ar = stream.BeginWrite(content, 0, content.Length, checker.DoCallback, "state");
        Equal("state", ar.AsyncState);
        await checker.Task;
        stream.EndWrite(ar);
        Equal(3, stream.Position);
        Equal(content, writer.WrittenMemory.ToArray());
    }

    [Fact]
    public static async Task SharedStreamConcurrentReadAsync()
    {
        byte[] expected = RandomBytes(512);

        await using var stream = StreamSource.AsSharedStream(new(expected));

        var task1 = ReadStreamAsync(stream);
        var task2 = ReadStreamAsync(stream);

        await Task.WhenAll(task1, task2);

        Equal(expected, task1.Result);
        Equal(expected, task2.Result);

        [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder<>))]
        static async Task<byte[]> ReadStreamAsync(Stream source)
        {
            source.Position = 0L;
            await Task.Yield();

            var result = new byte[source.Length];
            await source.ReadExactlyAsync(result);
            return result;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void SharedStreamConcurrentRead(bool compatWithAsync)
    {
        byte[] expected = RandomBytes(512);

        using var stream = StreamSource.AsSharedStream(new(expected), compatWithAsync);

        var thread1 = new Thread(ReadStream);
        var thread2 = new Thread(ReadStream);

        thread1.Start();
        thread2.Start();

        thread1.Join();
        thread2.Join();

        void ReadStream()
        {
            stream.Position = 0L;

            var actual = new byte[stream.Length];
            stream.ReadExactly(actual);
            Equal(expected, actual);
        }
    }

    [Fact]
    public static void SyncOverAsyncRead()
    {
        var expected = RandomBytes(512);
        using var source = new MemoryStream();
        source.Write(expected);
        source.Seek(0L, SeekOrigin.Begin);

        using var destination = StreamSource.AsStream(ReadAsync, source);
        True(destination.CanTimeout);
        destination.ReadTimeout = (int)DefaultTimeout.TotalMilliseconds;
        var actual = new byte[512];
        Equal(expected.Length, destination.Read(actual, 0, actual.Length));
        Equal(expected, actual);

        static ValueTask<int> ReadAsync(Memory<byte> buffer, MemoryStream ms, CancellationToken token)
            => ms.ReadAsync(buffer, token);
    }
}