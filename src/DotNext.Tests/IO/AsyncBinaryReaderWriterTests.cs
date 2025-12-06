using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Text;
using static System.Globalization.CultureInfo;
using DateTimeStyles = System.Globalization.DateTimeStyles;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.IO;

using Buffers;
using Buffers.Binary;
using IO.Pipelines;
using Text;

public sealed class AsyncBinaryReaderWriterTests : Test
{
    public interface IAsyncBinaryReaderWriterSource : IAsyncDisposable
    {
        IAsyncBinaryWriter CreateWriter();

        IAsyncBinaryReader CreateReader();
    }

    private sealed class DefaultSource : IAsyncBinaryReaderWriterSource
    {
        private sealed class DefaultAsyncBinaryReader(Stream stream, Memory<byte> buffer) : IAsyncBinaryReader
        {
            private readonly IAsyncBinaryReader reader = IAsyncBinaryReader.Create(stream, buffer);

            ValueTask<TReader> IAsyncBinaryReader.ReadAsync<TReader>(TReader reader, CancellationToken token)
                => this.reader.ReadAsync(reader, token);

            ValueTask IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, long? count, CancellationToken token)
                => reader.CopyToAsync(consumer, count, token);
        }

        private sealed class DefaultAsyncBinaryWriter(Stream stream, Memory<byte> buffer) : IAsyncBinaryWriter
        {
            private readonly IAsyncBinaryWriter writer = IAsyncBinaryWriter.Create(stream, buffer);

            Memory<byte> IAsyncBinaryWriter.Buffer => writer.Buffer;

            ValueTask IAsyncBinaryWriter.AdvanceAsync(int bytesWritten, CancellationToken token)
                => writer.AdvanceAsync(bytesWritten, token);
        }

        private readonly MemoryStream stream = new();
        private readonly byte[] buffer = new byte[512];

        public IAsyncBinaryReader CreateReader()
        {
            stream.Position = 0;
            return new DefaultAsyncBinaryReader(stream, buffer);
        }

        public IAsyncBinaryWriter CreateWriter() => new DefaultAsyncBinaryWriter(stream, buffer);

        public ValueTask DisposeAsync() => stream.DisposeAsync();
    }

    private sealed class BufferSource : IAsyncBinaryReaderWriterSource
    {
        private readonly PoolingBufferWriter<byte> buffer = new();

        public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(buffer);

        public IAsyncBinaryReader CreateReader() => new SequenceReader(buffer.WrittenMemory);

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            buffer.Dispose();
            return new ValueTask();
        }
    }

    private sealed class StreamSource : IAsyncBinaryReaderWriterSource
    {
        private readonly MemoryStream stream = new(1024);
        private readonly byte[] buffer = new byte[512];

        public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(stream, buffer);

        public IAsyncBinaryReader CreateReader()
        {
            stream.Position = 0L;
            return IAsyncBinaryReader.Create(stream, buffer);
        }

        public ValueTask DisposeAsync() => stream.DisposeAsync();
    }

    private sealed class PipeSource : IAsyncBinaryReaderWriterSource, IFlushable
    {
        private readonly Pipe pipe = new();

        public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(pipe.Writer, bufferSize: 16L);

        public IAsyncBinaryReader CreateReader() => IAsyncBinaryReader.Create(pipe.Reader);

        internal ValueTask CompleteWriterAsync() => pipe.Writer.CompleteAsync();

        public Task FlushAsync(CancellationToken token)
            => pipe.Writer.FlushAsync(token).AsTask();

        void IFlushable.Flush() => throw new NotSupportedException();

        public async ValueTask DisposeAsync()
        {
            await pipe.Writer.CompleteAsync();
            await pipe.Reader.CompleteAsync();
            pipe.Reset();
        }
    }

    private sealed class ReadOnlySequenceSource : IAsyncBinaryReaderWriterSource
    {
        private readonly MemoryStream stream = new(1024);
        private readonly byte[] buffer = new byte[512];

        public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(stream, buffer);

        public IAsyncBinaryReader CreateReader()
        {
            stream.Position = 0L;
            var sequence = ToReadOnlySequence<byte>(stream.ToArray(), 3);
            return new SequenceReader(sequence);
        }

        public ValueTask DisposeAsync() => stream.DisposeAsync();
    }

    private sealed class FileSource : Disposable, IAsyncBinaryReaderWriterSource, IFlushable
    {
        private readonly SafeFileHandle handle;
        private readonly FileWriter writer;
        private readonly FileReader reader;

        public FileSource(int bufferSize)
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            writer = new(handle) { MaxBufferSize = bufferSize };
            reader = new(handle) { MaxBufferSize = bufferSize };
        }

        public IAsyncBinaryWriter CreateWriter() => writer;

        public IAsyncBinaryReader CreateReader() => reader;

        public Task FlushAsync(CancellationToken token) => writer.WriteAsync(token).AsTask();

        void IFlushable.Flush() => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                writer.Dispose();
                reader.Dispose();
                handle.Dispose();
            }

            base.Dispose(disposing);
        }

        public new ValueTask DisposeAsync() => base.DisposeAsync();
    }

    public static TheoryData<IAsyncBinaryReaderWriterSource, Encoding> GetDataForPrimitives() => new()
    {
        { new FileSource(128), Encoding.UTF8 },
        { new FileSource(1024), Encoding.UTF8 },
        { new StreamSource(), Encoding.UTF8 },
        { new PipeSource(), Encoding.UTF8 },
        { new BufferSource(), Encoding.UTF8 },
        { new ReadOnlySequenceSource(), Encoding.UTF8 },
        { new DefaultSource(), Encoding.UTF8 },
        { new FileSource(128), Encoding.Unicode },
        { new FileSource(1024), Encoding.Unicode },
        { new StreamSource(), Encoding.Unicode },
        { new PipeSource(), Encoding.Unicode },
        { new BufferSource(), Encoding.Unicode },
        { new ReadOnlySequenceSource(), Encoding.Unicode },
        { new DefaultSource(), Encoding.Unicode }
    };

    [Theory]
    [MemberData(nameof(GetDataForPrimitives))]
    public static async Task WriteReadPrimitivesAsync(IAsyncBinaryReaderWriterSource source, Encoding encoding)
    {
        await using (source)
        {
            const byte value8 = 254;
            const short value16 = 42;
            const int value32 = int.MaxValue;
            const long value64 = long.MaxValue;
            const decimal valueM = 42M;
            const float valueF = 56.6F;
            const double valueD = 67.7D;
            var valueG = Guid.NewGuid();
            var valueDT = DateTime.Now;
            var valueDTO = DateTimeOffset.Now;
            var valueT = TimeSpan.FromMilliseconds(1024);
            var blob = RandomBytes(128);
            var memberId = new Net.Cluster.ClusterMemberId(Random.Shared);

            var writer = source.CreateWriter();
            await writer.WriteLittleEndianAsync(value16, TestToken);
            await writer.WriteLittleEndianAsync(value32, TestToken);
            await writer.WriteBigEndianAsync(value64, TestToken);
            var encodingContext = new EncodingContext(encoding, true);
            await writer.FormatAsync(value8, encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture, token: TestToken);
            await writer.FormatAsync(value16, encodingContext, LengthFormat.Compressed, provider: InvariantCulture, token: TestToken);
            await writer.FormatAsync(value32, encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture, token: TestToken);
            await writer.FormatAsync(value64, encodingContext, LengthFormat.BigEndian, provider: InvariantCulture, token: TestToken);
            await writer.FormatAsync(valueM, encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture, token: TestToken);
            await writer.FormatAsync(valueF, encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture, token: TestToken);
            await writer.FormatAsync(valueD, encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture, token: TestToken);
            await writer.FormatAsync(valueG, encodingContext, LengthFormat.LittleEndian, token: TestToken);
            await writer.FormatAsync(valueG, encodingContext, LengthFormat.LittleEndian, "X", token: TestToken);
            await writer.FormatAsync(valueDT, encodingContext, LengthFormat.LittleEndian, format: "O", provider: InvariantCulture, token: TestToken);
            await writer.FormatAsync(valueDTO, encodingContext, LengthFormat.LittleEndian, format: "O", provider: InvariantCulture, token: TestToken);
            await writer.FormatAsync(valueDT, encodingContext, LengthFormat.LittleEndian, format: "O", provider: InvariantCulture, token: TestToken);
            await writer.FormatAsync(valueDTO, encodingContext, LengthFormat.LittleEndian, format: "O", provider: InvariantCulture, token: TestToken);
            await writer.FormatAsync(valueT, encodingContext, LengthFormat.LittleEndian, token: TestToken);
            await writer.FormatAsync(valueT, encodingContext, LengthFormat.LittleEndian, "G", InvariantCulture, token: TestToken);
            await writer.WriteAsync(blob, LengthFormat.Compressed, TestToken);
            await writer.WriteAsync(memberId, TestToken);

            // UTF-8
            await writer.FormatAsync(value8, LengthFormat.Compressed, format: "X", provider: InvariantCulture, token: TestToken);
            await writer.FormatAsync(value16, LengthFormat.BigEndian, provider: InvariantCulture, token: TestToken);

            if (source is IFlushable flushable)
                await flushable.FlushAsync(TestToken);

            var reader = source.CreateReader();
            Equal(value16, await reader.ReadLittleEndianAsync<short>(TestToken));
            Equal(value32, await reader.ReadLittleEndianAsync<int>(TestToken));
            Equal(value64, await reader.ReadBigEndianAsync<long>(TestToken));
            var decodingContext = new DecodingContext(encoding, true);
            Equal(value8, await reader.ParseAsync<byte>(decodingContext, LengthFormat.LittleEndian, NumberStyles.Integer, InvariantCulture, token: TestToken));
            Equal(value16, await reader.ParseAsync<short>(decodingContext, LengthFormat.Compressed, NumberStyles.Integer, InvariantCulture, token: TestToken));
            Equal(value32, await reader.ParseAsync<IFormatProvider, int>(InvariantCulture, int.Parse, decodingContext, LengthFormat.LittleEndian, token: TestToken));
            Equal(value64, await reader.ParseAsync<IFormatProvider, long>(InvariantCulture, long.Parse, decodingContext, LengthFormat.BigEndian, token: TestToken));
            Equal(valueM, await reader.ParseAsync<decimal>(decodingContext, LengthFormat.LittleEndian, NumberStyles.Float, InvariantCulture,  token: TestToken));
            Equal(valueF, await reader.ParseAsync<float>(decodingContext, LengthFormat.LittleEndian, NumberStyles.Float, InvariantCulture, token: TestToken));
            Equal(valueD, await reader.ParseAsync<double>(decodingContext, LengthFormat.LittleEndian, NumberStyles.Float, InvariantCulture, token: TestToken));
            Equal(valueG, await reader.ParseAsync(InvariantCulture, Guid.Parse, decodingContext, LengthFormat.LittleEndian, token: TestToken));
            Equal(valueG, await reader.ParseAsync(InvariantCulture, static (c, _) => Guid.ParseExact(c, "X"), decodingContext, LengthFormat.LittleEndian, token: TestToken));
            Equal(valueDT, await reader.ParseAsync(InvariantCulture, static (c, p) => DateTime.Parse(c, p, DateTimeStyles.RoundtripKind), decodingContext, LengthFormat.LittleEndian, token: TestToken));
            Equal(valueDTO, await reader.ParseAsync(InvariantCulture, static (c, p) => DateTimeOffset.Parse(c, p, DateTimeStyles.RoundtripKind), decodingContext, LengthFormat.LittleEndian, token: TestToken));
            Equal(valueDT, await reader.ParseAsync(InvariantCulture, static (c, p) => DateTime.ParseExact(c, "O", p, DateTimeStyles.RoundtripKind), decodingContext, LengthFormat.LittleEndian, token: TestToken));
            Equal(valueDTO, await reader.ParseAsync(InvariantCulture, static (c, p) => DateTimeOffset.ParseExact(c, "O", p, DateTimeStyles.RoundtripKind), decodingContext, LengthFormat.LittleEndian, token: TestToken));
            Equal(valueT, await reader.ParseAsync(InvariantCulture, TimeSpan.Parse, decodingContext, LengthFormat.LittleEndian, token: TestToken));
            Equal(valueT, await reader.ParseAsync(InvariantCulture, static (c, p) => TimeSpan.ParseExact(c, "G", p), decodingContext, LengthFormat.LittleEndian, token: TestToken));
            using var decodedBlob = await reader.ReadAsync(LengthFormat.Compressed, token: TestToken);
            Equal(blob, decodedBlob.Memory.ToArray());
            Equal(memberId, await reader.ReadAsync<Net.Cluster.ClusterMemberId>(TestToken));

            // UTF-8
            Equal(value8, await reader.ParseAsync<byte>(LengthFormat.Compressed, NumberStyles.HexNumber, InvariantCulture, TestToken));
            Equal(value16, await reader.ParseAsync<short>(LengthFormat.BigEndian, InvariantCulture, TestToken));
        }
    }

    public static TheoryData<IAsyncBinaryReaderWriterSource, Encoding, LengthFormat> GetDataForStringEncoding() => new()
    {
        { new StreamSource(), Encoding.UTF8, LengthFormat.Compressed },
        { new StreamSource(), Encoding.UTF8, LengthFormat.LittleEndian },
        { new StreamSource(), Encoding.UTF8, LengthFormat.BigEndian },
        { new StreamSource(), Encoding.UTF8, LengthFormat.LittleEndian },

        { new BufferSource(), Encoding.UTF8, LengthFormat.Compressed },
        { new BufferSource(), Encoding.UTF8, LengthFormat.LittleEndian },
        { new BufferSource(), Encoding.UTF8, LengthFormat.BigEndian },
        { new BufferSource(), Encoding.UTF8, LengthFormat.LittleEndian },

        { new PipeSource(), Encoding.UTF8, LengthFormat.Compressed },
        { new PipeSource(), Encoding.UTF8, LengthFormat.LittleEndian },
        { new PipeSource(), Encoding.UTF8, LengthFormat.BigEndian },
        { new PipeSource(), Encoding.UTF8, LengthFormat.LittleEndian },

        { new ReadOnlySequenceSource(), Encoding.UTF8, LengthFormat.Compressed },
        { new ReadOnlySequenceSource(), Encoding.UTF8, LengthFormat.LittleEndian },
        { new ReadOnlySequenceSource(), Encoding.UTF8, LengthFormat.BigEndian },
        { new ReadOnlySequenceSource(), Encoding.UTF8, LengthFormat.LittleEndian },
    };

    [Theory]
    [MemberData(nameof(GetDataForStringEncoding))]
    public static async Task WriteReadStringAsync(IAsyncBinaryReaderWriterSource source, Encoding encoding, LengthFormat lengthFormat)
    {
        await using (source)
        {
            const string value = "Hello, world!&*(@&*(fghjwgfwffgw Привет, мир!";
            var writer = source.CreateWriter();
            await writer.EncodeAsync(value.AsMemory(), encoding, lengthFormat, TestToken);

            if (source is IFlushable)
                await ((IFlushable)source).FlushAsync(TestToken);

            var reader = source.CreateReader();
            using var result = await reader.DecodeAsync(encoding, lengthFormat, token: TestToken);
            Equal(value, result.ToString());
        }
    }

    [Theory]
    [MemberData(nameof(GetDataForStringEncoding))]
    public static async Task WriteReadString2Async(IAsyncBinaryReaderWriterSource source, Encoding encoding, LengthFormat lengthFormat)
    {
        await using (source)
        {
            const string value = "Hello, world!&*(@&*(fghjwgfwffgw Привет, мир!";
            var writer = source.CreateWriter();
            await writer.EncodeAsync(value.AsMemory(), encoding, lengthFormat, TestToken);

            if (source is IFlushable)
                await ((IFlushable)source).FlushAsync(TestToken);

            var reader = source.CreateReader();
            Memory<char> charBuffer = new char[9];

            var bufferWriter = new ArrayBufferWriter<char>(value.Length);
            await foreach (var chunk in reader.DecodeAsync(encoding, lengthFormat, charBuffer, TestToken))
                bufferWriter.Write(chunk.Span);

            Equal(value, bufferWriter.WrittenSpan.ToString());
        }
    }

    public static TheoryData<IAsyncBinaryReaderWriterSource> GetSources() => new()
    {
        new StreamSource(),
        new PipeSource(),
        new BufferSource(),
        new DefaultSource(),
    };

    [Theory]
    [MemberData(nameof(GetSources))]
    public static async Task CopyFromStreamToStream(IAsyncBinaryReaderWriterSource source)
    {
        var content = new byte[] { 1, 2, 3 };

        await using (source)
        {
            using (var sourceStream = new MemoryStream(content, false))
            {
                var writer = source.CreateWriter();
                await writer.CopyFromAsync(sourceStream, token: TestToken);
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                using var destStream = new MemoryStream(256);
                await reader.CopyToAsync(destStream, token: TestToken);
                Equal(content, destStream.ToArray());
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetSources))]
    public static async Task CopyFromStreamToStreamWithLength(IAsyncBinaryReaderWriterSource source)
    {
        var content = new byte[] { 1, 2, 3 };

        await using (source)
        {
            using var sourceStream = new MemoryStream(content, false);
            var writer = source.CreateWriter();
            await writer.CopyFromAsync(sourceStream, sourceStream.Length, TestToken);
            if (source is PipeSource pipe)
                await pipe.CompleteWriterAsync();

            var reader = source.CreateReader();
            using var destStream = new MemoryStream(256);
            await reader.CopyToAsync(destStream, sourceStream.Length, TestToken);
            Equal(content, destStream.ToArray());
        }
    }

    [Theory]
    [MemberData(nameof(GetSources))]
    public static async Task CopyFromStreamToConsumer(IAsyncBinaryReaderWriterSource source)
    {
        var content = new byte[] { 1, 2, 3 };

        await using (source)
        {
            using (var sourceStream = new MemoryStream(content, false))
            {
                var writer = source.CreateWriter();
                await writer.CopyFromAsync(sourceStream, token: TestToken);
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                using var destStream = new MemoryStream(256);
                await reader.CopyToAsync<StreamConsumer>(destStream, token: TestToken);
                Equal(content, destStream.ToArray());
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetSources))]
    public static async Task CopyFromStreamToConsumerWithLength(IAsyncBinaryReaderWriterSource source)
    {
        var content = new byte[] { 1, 2, 3 };

        await using (source)
        {
            using var sourceStream = new MemoryStream(content, false);
            var writer = source.CreateWriter();
            await writer.CopyFromAsync(sourceStream, sourceStream.Length, TestToken);
            if (source is PipeSource pipe)
                await pipe.CompleteWriterAsync();

            var reader = source.CreateReader();
            using var destStream = new MemoryStream(256);
            await reader.CopyToAsync<StreamConsumer>(destStream, sourceStream.Length, TestToken);
            Equal(content, destStream.ToArray());
        }
    }

    [Theory]
    [MemberData(nameof(GetSources))]
    public static async Task CopyFromStreamToBuffer(IAsyncBinaryReaderWriterSource source)
    {
        var content = new byte[] { 1, 2, 3 };

        await using (source)
        {
            using (var sourceStream = new MemoryStream(content, false))
            {
                var writer = source.CreateWriter();
                await writer.CopyFromAsync(sourceStream, token: TestToken);
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                var destination = new ArrayBufferWriter<byte>(256);
                await reader.CopyToAsync(destination, token: TestToken);
                Equal(content, destination.WrittenSpan.ToArray());
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetSources))]
    public static async Task CopyFromStreamToBufferWithLength(IAsyncBinaryReaderWriterSource source)
    {
        var content = new byte[] { 1, 2, 3 };

        await using (source)
        {
            using var sourceStream = new MemoryStream(content, false);
            var writer = source.CreateWriter();
            await writer.CopyFromAsync(sourceStream, sourceStream.Length, TestToken);
            if (source is PipeSource pipe)
                await pipe.CompleteWriterAsync();

            var reader = source.CreateReader();
            var destination = new ArrayBufferWriter<byte>(256);
            await reader.CopyToAsync(destination, sourceStream.Length, TestToken);
            Equal(content, destination.WrittenSpan.ToArray());
        }
    }

    [Theory]
    [MemberData(nameof(GetSources))]
    public static async Task SkipContent(IAsyncBinaryReaderWriterSource source)
    {
        await using (source)
        {
            var writer = source.CreateWriter();
            await writer.WriteAsync(new byte[] { 1, 2, 3 }, token: TestToken);
            await writer.WriteAsync(new byte[] { 4, 5, 6 },  token: TestToken);
            if (source is PipeSource pipe)
                await pipe.CompleteWriterAsync();

            var reader = source.CreateReader();
            Memory<byte> buffer = new byte[3];
            await reader.SkipAsync(3, TestToken);
            await reader.ReadAsync(buffer, TestToken);
            Equal(4, buffer.Span[0]);
            Equal(5, buffer.Span[1]);
            Equal(6, buffer.Span[2]);
            await ThrowsAsync<EndOfStreamException>(() => reader.SkipAsync(3, TestToken).AsTask());
        }
    }

    [Fact]
    public static void EmptyReader()
    {
        var reader = IAsyncBinaryReader.Empty;
        True(reader.TryGetSequence(out var sequence));
        True(sequence.IsEmpty);
        True(reader.TryGetRemainingBytesCount(out var remainingCount));
        Equal(0L, remainingCount);
        True(reader.SkipAsync(0, TestToken).IsCompletedSuccessfully);
        True(reader.CopyToAsync(Stream.Null, token: TestToken).IsCompletedSuccessfully);
        True(reader.CopyToAsync(new ArrayBufferWriter<byte>(), token: TestToken).IsCompletedSuccessfully);
        True(reader.CopyToAsync(new StreamConsumer(Stream.Null), token: TestToken).IsCompletedSuccessfully);
        True(reader.ReadAsync<DummyReader>(new(false), TestToken).IsCompletedSuccessfully);
        Throws<EndOfStreamException>(() => reader.ReadAsync<Blittable<int>>(TestToken).Result);
        Throws<EndOfStreamException>(() => reader.ReadAsync(new byte[2].AsMemory(), TestToken).GetAwaiter().GetResult());
        Throws<EndOfStreamException>(() => reader.SkipAsync(10, TestToken).GetAwaiter().GetResult());
        True(reader.ReadAsync(Memory<byte>.Empty, TestToken).IsCompletedSuccessfully);
        Throws<EndOfStreamException>(() => reader.ReadLittleEndianAsync<short>(TestToken).Result);
        Throws<EndOfStreamException>(() => reader.ReadAsync<DummyReader>(new(true), TestToken).Result);

        Throws<EndOfStreamException>(() => reader.DecodeAsync(default, LengthFormat.LittleEndian, token: TestToken).Result);
        Throws<EndOfStreamException>(() => reader.ParseAsync<byte>(LengthFormat.LittleEndian, token: TestToken).Result);
        Throws<EndOfStreamException>(() => reader.ParseAsync<byte>(LengthFormat.LittleEndian, NumberStyles.Integer, token: TestToken).Result);
        Throws<EndOfStreamException>(() => reader.ParseAsync<byte>(default, LengthFormat.LittleEndian, NumberStyles.Integer, token: TestToken).Result);
        Throws<EndOfStreamException>(() => reader.ParseAsync<IFormatProvider, byte>(InvariantCulture, byte.Parse, default, LengthFormat.LittleEndian, token: TestToken).Result);
    }

    [Fact]
    public static async Task EmptyReaderAsync()
    {
        await using var ms = new MemoryStream();
        var reader = IAsyncBinaryReader.Empty;
        await reader.CopyToAsync(ms, token: TestToken);
        Equal(0, ms.Length);

        var writer = new ArrayBufferWriter<byte>();
        await reader.CopyToAsync(writer, token: TestToken);
        Equal(0, writer.WrittenCount);
    }

    private struct DummyReader(bool hasLength) : IBufferReader
    {
        readonly int IBufferReader.RemainingBytes => hasLength ? 1 : 0;

        readonly void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> span) => Fail("Should never be called");
    }
}