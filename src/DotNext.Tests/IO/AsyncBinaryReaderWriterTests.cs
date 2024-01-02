using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Text;
using static System.Globalization.CultureInfo;
using DateTimeStyles = System.Globalization.DateTimeStyles;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.IO;

using Buffers;
using DotNext.Buffers.Binary;
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

        public IAsyncBinaryReader CreateReader() => IAsyncBinaryReader.Create(buffer.WrittenMemory);

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

        void IFlushable.Flush()
        {
            using var timeoutSource = new CancellationTokenSource(DefaultTimeout);
            using var task = FlushAsync(timeoutSource.Token);
            task.Wait();
        }

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
            return IAsyncBinaryReader.Create(sequence);
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
            handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, FileOptions.Asynchronous);
            writer = new(handle, bufferSize: bufferSize);
            reader = new(handle, bufferSize: bufferSize);
        }

        public IAsyncBinaryWriter CreateWriter() => writer;

        public IAsyncBinaryReader CreateReader() => reader;

        public Task FlushAsync(CancellationToken token) => writer.WriteAsync(token).AsTask();

        void IFlushable.Flush()
        {
            using (var task = FlushAsync(CancellationToken.None))
                task.Wait(DefaultTimeout);
        }

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

    public static IEnumerable<object[]> GetDataForPrimitives()
    {
        yield return new object[] { new FileSource(128), Encoding.UTF8 };
        yield return new object[] { new FileSource(1024), Encoding.UTF8 };
        yield return new object[] { new StreamSource(), Encoding.UTF8 };
        yield return new object[] { new PipeSource(), Encoding.UTF8 };
        yield return new object[] { new BufferSource(), Encoding.UTF8 };
        yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF8 };
        yield return new object[] { new DefaultSource(), Encoding.UTF8 };

        yield return new object[] { new FileSource(128), Encoding.Unicode };
        yield return new object[] { new FileSource(1024), Encoding.Unicode };
        yield return new object[] { new StreamSource(), Encoding.Unicode };
        yield return new object[] { new PipeSource(), Encoding.Unicode };
        yield return new object[] { new BufferSource(), Encoding.Unicode };
        yield return new object[] { new ReadOnlySequenceSource(), Encoding.Unicode };
        yield return new object[] { new DefaultSource(), Encoding.Unicode };
    }

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
            await writer.WriteLittleEndianAsync(value16);
            await writer.WriteLittleEndianAsync(value32);
            await writer.WriteBigEndianAsync(value64);
            var encodingContext = new EncodingContext(encoding, true);
            await writer.FormatAsync(value8, encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture);
            await writer.FormatAsync(value16, encodingContext, LengthFormat.Compressed, provider: InvariantCulture);
            await writer.FormatAsync(value32, encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture);
            await writer.FormatAsync(value64, encodingContext, LengthFormat.BigEndian, provider: InvariantCulture);
            await writer.FormatAsync(valueM, encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture);
            await writer.FormatAsync(valueF, encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture);
            await writer.FormatAsync(valueD, encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture);
            await writer.FormatAsync(valueG, encodingContext, LengthFormat.LittleEndian);
            await writer.FormatAsync(valueG, encodingContext, LengthFormat.LittleEndian, "X");
            await writer.FormatAsync(valueDT, encodingContext, LengthFormat.LittleEndian, format: "O", provider: InvariantCulture);
            await writer.FormatAsync(valueDTO, encodingContext, LengthFormat.LittleEndian, format: "O", provider: InvariantCulture);
            await writer.FormatAsync(valueDT, encodingContext, LengthFormat.LittleEndian, format: "O", provider: InvariantCulture);
            await writer.FormatAsync(valueDTO, encodingContext, LengthFormat.LittleEndian, format: "O", provider: InvariantCulture);
            await writer.FormatAsync(valueT, encodingContext, LengthFormat.LittleEndian);
            await writer.FormatAsync(valueT, encodingContext, LengthFormat.LittleEndian, "G", InvariantCulture);
            await writer.WriteAsync(blob, LengthFormat.Compressed);
            await writer.WriteAsync(memberId);

            // UTF-8
            await writer.FormatAsync(value8, LengthFormat.Compressed, format: "X", provider: InvariantCulture);
            await writer.FormatAsync(value16, LengthFormat.BigEndian, provider: InvariantCulture);

            if (source is IFlushable flushable)
                await flushable.FlushAsync();

            var reader = source.CreateReader();
            Equal(value16, await reader.ReadLittleEndianAsync<short>());
            Equal(value32, await reader.ReadLittleEndianAsync<int>());
            Equal(value64, await reader.ReadBigEndianAsync<long>());
            var decodingContext = new DecodingContext(encoding, true);
            Equal(value8, await reader.ParseAsync<byte>(decodingContext, LengthFormat.LittleEndian, NumberStyles.Integer, InvariantCulture));
            Equal(value16, await reader.ParseAsync<short>(decodingContext, LengthFormat.Compressed, NumberStyles.Integer, InvariantCulture));
            Equal(value32, await reader.ParseAsync<IFormatProvider, int>(InvariantCulture, int.Parse, decodingContext, LengthFormat.LittleEndian));
            Equal(value64, await reader.ParseAsync<IFormatProvider, long>(InvariantCulture, long.Parse, decodingContext, LengthFormat.BigEndian));
            Equal(valueM, await reader.ParseAsync<decimal>(decodingContext, LengthFormat.LittleEndian, NumberStyles.Float, InvariantCulture));
            Equal(valueF, await reader.ParseAsync<float>(decodingContext, LengthFormat.LittleEndian, NumberStyles.Float, InvariantCulture));
            Equal(valueD, await reader.ParseAsync<double>(decodingContext, LengthFormat.LittleEndian, NumberStyles.Float, InvariantCulture));
            Equal(valueG, await reader.ParseAsync(InvariantCulture, Guid.Parse, decodingContext, LengthFormat.LittleEndian));
            Equal(valueG, await reader.ParseAsync(InvariantCulture, static (c, p) => Guid.ParseExact(c, "X"), decodingContext, LengthFormat.LittleEndian));
            Equal(valueDT, await reader.ParseAsync(InvariantCulture, static (c, p) => DateTime.Parse(c, p, DateTimeStyles.RoundtripKind), decodingContext, LengthFormat.LittleEndian));
            Equal(valueDTO, await reader.ParseAsync(InvariantCulture, static (c, p) => DateTimeOffset.Parse(c, p, DateTimeStyles.RoundtripKind), decodingContext, LengthFormat.LittleEndian));
            Equal(valueDT, await reader.ParseAsync(InvariantCulture, static (c, p) => DateTime.ParseExact(c, "O", p, DateTimeStyles.RoundtripKind), decodingContext, LengthFormat.LittleEndian));
            Equal(valueDTO, await reader.ParseAsync(InvariantCulture, static (c, p) => DateTimeOffset.ParseExact(c, "O", p, DateTimeStyles.RoundtripKind), decodingContext, LengthFormat.LittleEndian));
            Equal(valueT, await reader.ParseAsync(InvariantCulture, TimeSpan.Parse, decodingContext, LengthFormat.LittleEndian));
            Equal(valueT, await reader.ParseAsync(InvariantCulture, static (c, p) => TimeSpan.ParseExact(c, "G", p), decodingContext, LengthFormat.LittleEndian));
            using var decodedBlob = await reader.ReadAsync(LengthFormat.Compressed);
            Equal(blob, decodedBlob.Memory.ToArray());
            Equal(memberId, await reader.ReadAsync<Net.Cluster.ClusterMemberId>());

            // UTF-8
            Equal(value8, await reader.ParseAsync<byte>(LengthFormat.Compressed, NumberStyles.HexNumber, InvariantCulture));
            Equal(value16, await reader.ParseAsync<short>(LengthFormat.BigEndian, InvariantCulture));
        }
    }

    public static IEnumerable<object[]> GetDataForStringEncoding()
    {
        yield return new object[] { new StreamSource(), Encoding.UTF8, LengthFormat.Compressed };
        yield return new object[] { new StreamSource(), Encoding.UTF8, LengthFormat.LittleEndian };
        yield return new object[] { new StreamSource(), Encoding.UTF8, LengthFormat.BigEndian };
        yield return new object[] { new StreamSource(), Encoding.UTF8, LengthFormat.LittleEndian };

        yield return new object[] { new BufferSource(), Encoding.UTF8, LengthFormat.Compressed };
        yield return new object[] { new BufferSource(), Encoding.UTF8, LengthFormat.LittleEndian };
        yield return new object[] { new BufferSource(), Encoding.UTF8, LengthFormat.BigEndian };
        yield return new object[] { new BufferSource(), Encoding.UTF8, LengthFormat.LittleEndian };

        yield return new object[] { new PipeSource(), Encoding.UTF8, LengthFormat.Compressed };
        yield return new object[] { new PipeSource(), Encoding.UTF8, LengthFormat.LittleEndian };
        yield return new object[] { new PipeSource(), Encoding.UTF8, LengthFormat.BigEndian };
        yield return new object[] { new PipeSource(), Encoding.UTF8, LengthFormat.LittleEndian };

        yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF8, LengthFormat.Compressed };
        yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF8, LengthFormat.LittleEndian };
        yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF8, LengthFormat.BigEndian };
        yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF8, LengthFormat.LittleEndian };
    }

    [Theory]
    [MemberData(nameof(GetDataForStringEncoding))]
    public static async Task WriteReadStringAsync(IAsyncBinaryReaderWriterSource source, Encoding encoding, LengthFormat lengthFormat)
    {
        await using (source)
        {
            const string value = "Hello, world!&*(@&*(fghjwgfwffgw Привет, мир!";
            var writer = source.CreateWriter();
            await writer.EncodeAsync(value.AsMemory(), encoding, lengthFormat);

            if (source is IFlushable)
                await ((IFlushable)source).FlushAsync();

            var reader = source.CreateReader();
            using var result = await reader.DecodeAsync(encoding, lengthFormat);
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
            await writer.EncodeAsync(value.AsMemory(), encoding, lengthFormat);

            if (source is IFlushable)
                await ((IFlushable)source).FlushAsync();

            var reader = source.CreateReader();
            Memory<char> charBuffer = new char[9];

            var bufferWriter = new ArrayBufferWriter<char>(value.Length);
            await foreach (var chunk in reader.DecodeAsync(encoding, lengthFormat, charBuffer))
                bufferWriter.Write(chunk.Span);

            Equal(value, bufferWriter.WrittenSpan.ToString());
        }
    }

    public static IEnumerable<object[]> GetSources()
    {
        yield return new object[] { new StreamSource() };
        yield return new object[] { new PipeSource() };
        yield return new object[] { new BufferSource() };
        yield return new object[] { new DefaultSource() };
    }

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
                await writer.CopyFromAsync(sourceStream);
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                using var destStream = new MemoryStream(256);
                await reader.CopyToAsync(destStream);
                Equal(content, destStream.ToArray());
            }

            using (var sourceStream = new MemoryStream(content, false))
            {
                var writer = source.CreateWriter();
                await writer.CopyFromAsync(sourceStream, sourceStream.Length);
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                using var destStream = new MemoryStream(256);
                await reader.CopyToAsync(destStream, sourceStream.Length);
                Equal(content, destStream.ToArray());
            }
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
                await writer.CopyFromAsync(sourceStream);
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                using var destStream = new MemoryStream(256);
                await reader.CopyToAsync<StreamConsumer>(destStream);
                Equal(content, destStream.ToArray());
            }

            using (var sourceStream = new MemoryStream(content, false))
            {
                var writer = source.CreateWriter();
                await writer.CopyFromAsync(sourceStream, sourceStream.Length);
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                using var destStream = new MemoryStream(256);
                await reader.CopyToAsync<StreamConsumer>(destStream, sourceStream.Length);
                Equal(content, destStream.ToArray());
            }
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
                await writer.CopyFromAsync(sourceStream);
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                var destination = new ArrayBufferWriter<byte>(256);
                await reader.CopyToAsync(destination);
                Equal(content, destination.WrittenSpan.ToArray());
            }

            using (var sourceStream = new MemoryStream(content, false))
            {
                var writer = source.CreateWriter();
                await writer.CopyFromAsync(sourceStream, sourceStream.Length);
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                var destination = new ArrayBufferWriter<byte>(256);
                await reader.CopyToAsync(destination, sourceStream.Length);
                Equal(content, destination.WrittenSpan.ToArray());
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetSources))]
    public static async Task SkipContent(IAsyncBinaryReaderWriterSource source)
    {
        await using (source)
        {
            var writer = source.CreateWriter();
            await writer.WriteAsync(new byte[] { 1, 2, 3 });
            await writer.WriteAsync(new byte[] { 4, 5, 6 });
            if (source is PipeSource pipe)
                await pipe.CompleteWriterAsync();

            var reader = source.CreateReader();
            Memory<byte> buffer = new byte[3];
            await reader.SkipAsync(3);
            await reader.ReadAsync(buffer);
            Equal(4, buffer.Span[0]);
            Equal(5, buffer.Span[1]);
            Equal(6, buffer.Span[2]);
            await ThrowsAsync<EndOfStreamException>(() => reader.SkipAsync(3).AsTask());
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
        True(reader.SkipAsync(0).IsCompletedSuccessfully);
        True(reader.CopyToAsync(Stream.Null).IsCompletedSuccessfully);
        True(reader.CopyToAsync(new ArrayBufferWriter<byte>()).IsCompletedSuccessfully);
        True(reader.CopyToAsync(new StreamConsumer(Stream.Null)).IsCompletedSuccessfully);
        True(reader.ReadAsync<DummyReader>(new(false)).IsCompletedSuccessfully);
        Throws<EndOfStreamException>(() => reader.ReadAsync<Blittable<int>>().Result);
        Throws<EndOfStreamException>(() => reader.ReadAsync(new byte[2].AsMemory()).GetAwaiter().GetResult());
        Throws<EndOfStreamException>(() => reader.SkipAsync(10).GetAwaiter().GetResult());
        True(reader.ReadAsync(Memory<byte>.Empty).IsCompletedSuccessfully);
        Throws<EndOfStreamException>(() => reader.ReadLittleEndianAsync<short>().Result);
        Throws<EndOfStreamException>(() => reader.ReadAsync<DummyReader>(new(true)).Result);

        Throws<EndOfStreamException>(() => reader.DecodeAsync(default, LengthFormat.LittleEndian).Result);
        Throws<EndOfStreamException>(() => reader.ParseAsync<byte>(LengthFormat.LittleEndian).Result);
        Throws<EndOfStreamException>(() => reader.ParseAsync<byte>(LengthFormat.LittleEndian, NumberStyles.Integer).Result);
        Throws<EndOfStreamException>(() => reader.ParseAsync<byte>(default, LengthFormat.LittleEndian, NumberStyles.Integer).Result);
        Throws<EndOfStreamException>(() => reader.ParseAsync<IFormatProvider, byte>(InvariantCulture, byte.Parse, default, LengthFormat.LittleEndian).Result);
    }

    [Fact]
    public static async Task EmptyReaderAsync()
    {
        await using var ms = new MemoryStream();
        var reader = IAsyncBinaryReader.Empty;
        await reader.CopyToAsync(ms);
        Equal(0, ms.Length);

        var writer = new ArrayBufferWriter<byte>();
        await reader.CopyToAsync(writer);
        Equal(0, writer.WrittenCount);
    }

    private struct DummyReader(bool hasLength) : IBufferReader
    {
        readonly int IBufferReader.RemainingBytes => hasLength ? 1 : 0;

        readonly void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> span) => Fail("Should never be called");
    }
}