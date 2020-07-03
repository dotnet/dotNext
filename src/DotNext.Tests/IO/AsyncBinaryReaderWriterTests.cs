using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using static System.Globalization.CultureInfo;
using DateTimeStyles = System.Globalization.DateTimeStyles;

namespace DotNext.IO
{
    using Text;
    using ChunkSequence = Buffers.ChunkSequence<byte>;
    using static Buffers.MemoryAllocator;

    public sealed class AsyncBinaryReaderWriterTests : Test
    {
        public interface IAsyncBinaryReaderWriterSource : IAsyncDisposable
        {
            IAsyncBinaryWriter CreateWriter();

            IAsyncBinaryReader CreateReader();
        }

        private sealed class BufferSource : IAsyncBinaryReaderWriterSource
        {
            private readonly MemoryStream stream = new MemoryStream();

            public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(stream.AsBufferWriter(ArrayPool<byte>.Shared.ToAllocator()));

            public IAsyncBinaryReader CreateReader() => IAsyncBinaryReader.Create(stream.ToArray());
            
            ValueTask IAsyncDisposable.DisposeAsync() => stream.DisposeAsync();
        }

        private sealed class StreamSource : IAsyncBinaryReaderWriterSource
        {
            private readonly MemoryStream stream = new MemoryStream(1024);
            private readonly byte[] buffer = new byte[512];

            public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(stream, buffer);

            public IAsyncBinaryReader CreateReader()
            {
                stream.Position = 0L;
                return IAsyncBinaryReader.Create(stream, buffer);
            }

            public ValueTask DisposeAsync() => stream.DisposeAsync();
        }

        private sealed class PipeSource : IAsyncBinaryReaderWriterSource
        {
            private readonly Pipe pipe = new Pipe();

            public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(pipe.Writer);

            public IAsyncBinaryReader CreateReader() => IAsyncBinaryReader.Create(pipe.Reader);

            public async ValueTask DisposeAsync()
            {
                await pipe.Writer.CompleteAsync();
                await pipe.Reader.CompleteAsync();
                pipe.Reset();
            }
        }

        private sealed class PipeSourceWithSettings : IAsyncBinaryReaderWriterSource
        {
            private readonly Pipe pipe = new Pipe();

            public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(pipe.Writer, 1024, 128);

            public IAsyncBinaryReader CreateReader() => IAsyncBinaryReader.Create(pipe.Reader);

            public async ValueTask DisposeAsync()
            {
                await pipe.Writer.CompleteAsync();
                await pipe.Reader.CompleteAsync();
                pipe.Reset();
            }
        }

        private sealed class ReadOnlySequenceSource : IAsyncBinaryReaderWriterSource
        {
            private readonly MemoryStream stream = new MemoryStream(1024);
            private readonly byte[] buffer = new byte[512];

            public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(stream, buffer);

            public IAsyncBinaryReader CreateReader()
            {
                stream.Position = 0L;
                var sequence = new ChunkSequence(stream.ToArray(), 3);
                return IAsyncBinaryReader.Create(sequence.ToReadOnlySequence());
            }

            public ValueTask DisposeAsync() => stream.DisposeAsync();
        }

        public static IEnumerable<object[]> GetDataForPrimitives()
        {
            yield return new object[] { new StreamSource(), true, Encoding.UTF8 };
            yield return new object[] { new StreamSource(), false, Encoding.UTF8 };
            yield return new object[] { new PipeSource(), true, Encoding.UTF8 };
            yield return new object[] { new PipeSource(), false, Encoding.UTF8 };
            yield return new object[] { new BufferSource(), true, Encoding.UTF8 };
            yield return new object[] { new BufferSource(), false, Encoding.UTF8 };
            yield return new object[] { new PipeSourceWithSettings(), true, Encoding.UTF8 };
            yield return new object[] { new PipeSourceWithSettings(), false, Encoding.UTF8 };
            yield return new object[] { new ReadOnlySequenceSource(), true, Encoding.UTF8 };
            yield return new object[] { new ReadOnlySequenceSource(), false, Encoding.UTF8 };

            yield return new object[] { new StreamSource(), true, Encoding.Unicode };
            yield return new object[] { new StreamSource(), false, Encoding.Unicode };
            yield return new object[] { new PipeSource(), true, Encoding.Unicode };
            yield return new object[] { new PipeSource(), false, Encoding.Unicode };
            yield return new object[] { new BufferSource(), true, Encoding.Unicode };
            yield return new object[] { new BufferSource(), false, Encoding.Unicode };
            yield return new object[] { new PipeSourceWithSettings(), true, Encoding.Unicode };
            yield return new object[] { new PipeSourceWithSettings(), false, Encoding.Unicode };
            yield return new object[] { new ReadOnlySequenceSource(), true, Encoding.Unicode };
            yield return new object[] { new ReadOnlySequenceSource(), false, Encoding.Unicode };
        }

        [Theory]
        [MemberData(nameof(GetDataForPrimitives))]
        public static async Task WriteReadPrimitivesAsync(IAsyncBinaryReaderWriterSource source, bool littleEndian, Encoding encoding)
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

                var writer = source.CreateWriter();
                await writer.WriteInt16Async(value16, littleEndian);
                await writer.WriteInt32Async(value32, littleEndian);
                await writer.WriteInt64Async(value64, littleEndian);
                await writer.WriteAsync(valueM);
                var encodingContext = new EncodingContext(encoding, true);
                await writer.WriteByteAsync(value8, StringLengthEncoding.Plain, encodingContext, provider: InvariantCulture);
                await writer.WriteInt16Async(value16, StringLengthEncoding.Compressed, encodingContext, provider: InvariantCulture);
                await writer.WriteInt32Async(value32, StringLengthEncoding.Plain, encodingContext, provider: InvariantCulture);
                await writer.WriteInt64Async(value64, StringLengthEncoding.PlainBigEndian, encodingContext, provider: InvariantCulture);
                await writer.WriteDecimalAsync(valueM, StringLengthEncoding.PlainLittleEndian, encodingContext, provider: InvariantCulture);
                await writer.WriteSingleAsync(valueF, StringLengthEncoding.Plain, encodingContext, provider: InvariantCulture);
                await writer.WriteDoubleAsync(valueD, StringLengthEncoding.Plain, encodingContext, provider: InvariantCulture);
                await writer.WriteGuidAsync(valueG, StringLengthEncoding.Plain, encodingContext);
                await writer.WriteGuidAsync(valueG, StringLengthEncoding.Plain, encodingContext, "X");
                await writer.WriteDateTimeAsync(valueDT, StringLengthEncoding.Plain, encodingContext, format: "O", provider: InvariantCulture);
                await writer.WriteDateTimeOffsetAsync(valueDTO, StringLengthEncoding.Plain, encodingContext, format: "O", provider: InvariantCulture);
                await writer.WriteDateTimeAsync(valueDT, StringLengthEncoding.Plain, encodingContext, format: "O", provider: InvariantCulture);
                await writer.WriteDateTimeOffsetAsync(valueDTO, StringLengthEncoding.Plain, encodingContext, format: "O", provider: InvariantCulture);

                var reader = source.CreateReader();
                Equal(value16, await reader.ReadInt16Async(littleEndian));
                Equal(value32, await reader.ReadInt32Async(littleEndian));
                Equal(value64, await reader.ReadInt64Async(littleEndian));
                Equal(valueM, await reader.ReadAsync<decimal>());
                var decodingContext = new DecodingContext(encoding, true);
                Equal(value8, await reader.ReadByteAsync(StringLengthEncoding.Plain, decodingContext, provider: InvariantCulture));
                Equal(value16, await reader.ReadInt16Async(StringLengthEncoding.Compressed, decodingContext, provider: InvariantCulture));
                Equal(value32, await reader.ReadInt32Async(StringLengthEncoding.Plain, decodingContext, provider: InvariantCulture));
                Equal(value64, await reader.ReadInt64Async(StringLengthEncoding.PlainBigEndian, decodingContext, provider: InvariantCulture));
                Equal(valueM, await reader.ReadDecimalAsync(StringLengthEncoding.PlainLittleEndian, decodingContext, provider: InvariantCulture));
                Equal(valueF, await reader.ReadSingleAsync(StringLengthEncoding.Plain, decodingContext, provider: InvariantCulture));
                Equal(valueD, await reader.ReadDoubleAsync(StringLengthEncoding.Plain, decodingContext, provider: InvariantCulture));
                Equal(valueG, await reader.ReadGuidAsync(StringLengthEncoding.Plain, decodingContext));
                Equal(valueG, await reader.ReadGuidAsync(StringLengthEncoding.Plain, decodingContext, "X"));
                Equal(valueDT, await reader.ReadDateTimeAsync(StringLengthEncoding.Plain, decodingContext, style: DateTimeStyles.RoundtripKind, provider: InvariantCulture));
                Equal(valueDTO, await reader.ReadDateTimeOffsetAsync(StringLengthEncoding.Plain, decodingContext, style: DateTimeStyles.RoundtripKind, provider: InvariantCulture));
                Equal(valueDT, await reader.ReadDateTimeAsync(StringLengthEncoding.Plain, decodingContext, new[] { "O" }, style: DateTimeStyles.RoundtripKind, provider: InvariantCulture));
                Equal(valueDTO, await reader.ReadDateTimeOffsetAsync(StringLengthEncoding.Plain, decodingContext, new[] { "O" }, style: DateTimeStyles.RoundtripKind, provider: InvariantCulture));
            }
        }

        public static IEnumerable<object[]> GetDataForStringEncoding()
        {
            yield return new object[] { new StreamSource(), Encoding.UTF8, StringLengthEncoding.Compressed };
            yield return new object[] { new StreamSource(), Encoding.UTF8, StringLengthEncoding.Plain };
            yield return new object[] { new StreamSource(), Encoding.UTF8, StringLengthEncoding.PlainBigEndian };
            yield return new object[] { new StreamSource(), Encoding.UTF8, StringLengthEncoding.PlainLittleEndian };
            yield return new object[] { new StreamSource(), Encoding.UTF8, null };

            yield return new object[] { new StreamSource(), Encoding.UTF7, StringLengthEncoding.Compressed };
            yield return new object[] { new StreamSource(), Encoding.UTF7, StringLengthEncoding.Plain };
            yield return new object[] { new StreamSource(), Encoding.UTF7, StringLengthEncoding.PlainBigEndian };
            yield return new object[] { new StreamSource(), Encoding.UTF7, StringLengthEncoding.PlainLittleEndian };
            yield return new object[] { new StreamSource(), Encoding.UTF7, null };

            yield return new object[] { new BufferSource(), Encoding.UTF8, StringLengthEncoding.Compressed };
            yield return new object[] { new BufferSource(), Encoding.UTF8, StringLengthEncoding.Plain };
            yield return new object[] { new BufferSource(), Encoding.UTF8, StringLengthEncoding.PlainBigEndian };
            yield return new object[] { new BufferSource(), Encoding.UTF8, StringLengthEncoding.PlainLittleEndian };
            yield return new object[] { new BufferSource(), Encoding.UTF8, null };

            yield return new object[] { new BufferSource(), Encoding.UTF7, StringLengthEncoding.Compressed };
            yield return new object[] { new BufferSource(), Encoding.UTF7, StringLengthEncoding.Plain };
            yield return new object[] { new BufferSource(), Encoding.UTF7, StringLengthEncoding.PlainBigEndian };
            yield return new object[] { new BufferSource(), Encoding.UTF7, StringLengthEncoding.PlainLittleEndian };
            yield return new object[] { new BufferSource(), Encoding.UTF7, null };

            yield return new object[] { new PipeSource(), Encoding.UTF8, StringLengthEncoding.Compressed };
            yield return new object[] { new PipeSource(), Encoding.UTF8, StringLengthEncoding.Plain };
            yield return new object[] { new PipeSource(), Encoding.UTF8, StringLengthEncoding.PlainBigEndian };
            yield return new object[] { new PipeSource(), Encoding.UTF8, StringLengthEncoding.PlainLittleEndian };
            yield return new object[] { new PipeSource(), Encoding.UTF8, null };

            yield return new object[] { new PipeSource(), Encoding.UTF7, StringLengthEncoding.Compressed };
            yield return new object[] { new PipeSource(), Encoding.UTF7, StringLengthEncoding.Plain };
            yield return new object[] { new PipeSource(), Encoding.UTF7, StringLengthEncoding.PlainBigEndian };
            yield return new object[] { new PipeSource(), Encoding.UTF7, StringLengthEncoding.PlainLittleEndian };
            yield return new object[] { new PipeSource(), Encoding.UTF7, null };

            yield return new object[] { new PipeSourceWithSettings(), Encoding.UTF8, StringLengthEncoding.Compressed };
            yield return new object[] { new PipeSourceWithSettings(), Encoding.UTF8, StringLengthEncoding.Plain };
            yield return new object[] { new PipeSourceWithSettings(), Encoding.UTF8, StringLengthEncoding.PlainBigEndian };
            yield return new object[] { new PipeSourceWithSettings(), Encoding.UTF8, StringLengthEncoding.PlainLittleEndian };
            yield return new object[] { new PipeSourceWithSettings(), Encoding.UTF8, null };

            yield return new object[] { new PipeSourceWithSettings(), Encoding.UTF7, StringLengthEncoding.Compressed };
            yield return new object[] { new PipeSourceWithSettings(), Encoding.UTF7, StringLengthEncoding.Plain };
            yield return new object[] { new PipeSourceWithSettings(), Encoding.UTF7, StringLengthEncoding.PlainBigEndian };
            yield return new object[] { new PipeSourceWithSettings(), Encoding.UTF7, StringLengthEncoding.PlainLittleEndian };
            yield return new object[] { new PipeSourceWithSettings(), Encoding.UTF7, null };

            yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF8, StringLengthEncoding.Compressed };
            yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF8, StringLengthEncoding.Plain };
            yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF8, StringLengthEncoding.PlainBigEndian };
            yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF8, StringLengthEncoding.PlainLittleEndian };
            yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF8, null };

            yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF7, StringLengthEncoding.Compressed };
            yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF7, StringLengthEncoding.Plain };
            yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF7, StringLengthEncoding.PlainBigEndian };
            yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF7, StringLengthEncoding.PlainLittleEndian };
            yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF7, null };
        }

        [Theory]
        [MemberData(nameof(GetDataForStringEncoding))]
        public static async Task WriteReadStringAsync(IAsyncBinaryReaderWriterSource source, Encoding encoding, StringLengthEncoding? lengthFormat)
        {
            await using (source)
            {
                const string value = "Hello, world!&*(@&*(fghjwgfwffgw ������, ���!";
                var writer = source.CreateWriter();
                await writer.WriteAsync(value.AsMemory(), encoding, lengthFormat);

                var reader = source.CreateReader();
                var result = await (lengthFormat is null ?
                    reader.ReadStringAsync(encoding.GetByteCount(value), encoding) :
                    reader.ReadStringAsync(lengthFormat.GetValueOrDefault(), encoding));
                Equal(value, result);
            }
        }
    }
}