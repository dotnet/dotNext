using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static System.Globalization.CultureInfo;
using DateTimeStyles = System.Globalization.DateTimeStyles;
using TimeSpanStyles = System.Globalization.TimeSpanStyles;

namespace DotNext.IO
{
    using Text;
    using static Buffers.MemoryAllocator;

    [ExcludeFromCodeCoverage]
    public sealed class AsyncBinaryReaderWriterTests : Test
    {
        public interface IAsyncBinaryReaderWriterSource : IAsyncDisposable
        {
            IAsyncBinaryWriter CreateWriter();

            IAsyncBinaryReader CreateReader();
        }

        private sealed class DefaultSource : IAsyncBinaryReaderWriterSource
        {
            private sealed class DefaultAsyncBinaryReader : IAsyncBinaryReader
            {
                private readonly IAsyncBinaryReader reader;

                internal DefaultAsyncBinaryReader(Stream stream, Memory<byte> buffer)
                    => reader = IAsyncBinaryReader.Create(stream, buffer);

                ValueTask<T> IAsyncBinaryReader.ReadAsync<T>(CancellationToken token)
                    => reader.ReadAsync<T>(token);
                
                ValueTask IAsyncBinaryReader.ReadAsync(Memory<byte> output, CancellationToken token)
                    => reader.ReadAsync(output, token);

                ValueTask<string> IAsyncBinaryReader.ReadStringAsync(int length, DecodingContext context, CancellationToken token)
                    => reader.ReadStringAsync(length, context, token);

                ValueTask<string> IAsyncBinaryReader.ReadStringAsync(StringLengthEncoding lengthFormat, DecodingContext context, CancellationToken token)
                    => reader.ReadStringAsync(lengthFormat, context, token);

                Task IAsyncBinaryReader.CopyToAsync(Stream output, CancellationToken token)
                    => reader.CopyToAsync(output, token);

                Task IAsyncBinaryReader.CopyToAsync(PipeWriter output, CancellationToken token)
                    => reader.CopyToAsync(output, token);
            }

            private sealed class DefaultAsyncBinaryWriter : IAsyncBinaryWriter
            {
                private readonly IAsyncBinaryWriter writer;

                internal DefaultAsyncBinaryWriter(Stream stream, Memory<byte> buffer)
                    => writer = IAsyncBinaryWriter.Create(stream, buffer);

                ValueTask IAsyncBinaryWriter.WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token)
                    => writer.WriteAsync(input, token);

                ValueTask IAsyncBinaryWriter.WriteAsync(ReadOnlyMemory<char> chars, EncodingContext context, StringLengthEncoding? lengthFormat, CancellationToken token)
                    => writer.WriteAsync(chars, context, lengthFormat, token);
            }

            private readonly MemoryStream stream = new MemoryStream();
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

            internal ValueTask CompleteWriterAsync() => pipe.Writer.CompleteAsync();

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
                var sequence = ToReadOnlySequence<byte>(stream.ToArray(), 3);
                return IAsyncBinaryReader.Create(sequence);
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
            yield return new object[] { new DefaultSource(), true, Encoding.UTF8 };
            yield return new object[] { new DefaultSource(), false, Encoding.UTF8 };

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
            yield return new object[] { new DefaultSource(), true, Encoding.Unicode };
            yield return new object[] { new DefaultSource(), false, Encoding.Unicode };
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
                var valueT = TimeSpan.FromMilliseconds(1024);

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
                await writer.WriteTimeSpanAsync(valueT, StringLengthEncoding.Plain, encodingContext);
                await writer.WriteTimeSpanAsync(valueT, StringLengthEncoding.Plain, encodingContext, "G", InvariantCulture);

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
                Equal(valueT, await reader.ReadTimeSpanAsync(StringLengthEncoding.Plain, decodingContext, InvariantCulture));
                Equal(valueT, await reader.ReadTimeSpanAsync(StringLengthEncoding.Plain, decodingContext, new[] { "G" }, TimeSpanStyles.None, InvariantCulture));
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

        public static IEnumerable<object[]> GetSourcesForCopy()
        {
            yield return new object[] { new StreamSource() };
            yield return new object[] { new PipeSource() };
            yield return new object[] { new BufferSource() };
            yield return new object[] { new DefaultSource() };
        }

        [Theory]
        [MemberData(nameof(GetSourcesForCopy))]
        public static async Task CopyFromStreamToStream(IAsyncBinaryReaderWriterSource source)
        {
            await using(source)
            {
                var content = new byte[] { 1, 2, 3};
                using var sourceStream = new MemoryStream(content, false);
                var writer = source.CreateWriter();
                await writer.CopyFromAsync(sourceStream);
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                using var destStream = new MemoryStream(256);
                await reader.CopyToAsync(destStream);
                Equal(content, destStream.ToArray());
            }
        }

        private sealed class MemorySource
        {
            internal readonly byte[] Content;
            private bool state;

            internal MemorySource(byte[] content) => Content = content;

            internal ReadOnlyMemory<byte> ReadContent()
            {
                if (state)
                    return default;
                state = true;
                return Content;
            }

            internal static ValueTask<ReadOnlyMemory<byte>> SupplyContent(MemorySource supplier, CancellationToken token)
                => new ValueTask<ReadOnlyMemory<byte>>(supplier.ReadContent());
        }

        [Theory]
        [MemberData(nameof(GetSourcesForCopy))]
        public static async Task CopyUsingSpanAction(IAsyncBinaryReaderWriterSource source)
        {
            await using(source)
            {
                var supplier = new MemorySource(new byte[] { 1, 2, 3});
                var writer = source.CreateWriter();
                await writer.CopyFromAsync(MemorySource.SupplyContent, supplier);
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                var consumer = new ArrayBufferWriter<byte>();
                await reader.CopyToAsync((span, writer) => writer.Write(span), consumer);
                Equal(supplier.Content, consumer.WrittenMemory.ToArray());
            }
        }

        [Theory]
        [MemberData(nameof(GetSourcesForCopy))]
        public static async Task CopyToBuffer(IAsyncBinaryReaderWriterSource source)
        {
            await using(source)
            {
                var supplier = new MemorySource(new byte[] { 1, 2, 3});
                var writer = source.CreateWriter();
                await writer.CopyFromAsync(MemorySource.SupplyContent, supplier);
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                var consumer = new ArrayBufferWriter<byte>();
                await reader.CopyToAsync(consumer);
                Equal(supplier.Content, consumer.WrittenMemory.ToArray());
            }
        }

        [Theory]
        [MemberData(nameof(GetSourcesForCopy))]
        public static async Task CopyUsingAsyncFunc(IAsyncBinaryReaderWriterSource source)
        {
            await using(source)
            {
                var supplier = new MemorySource(new byte[] { 1, 2, 3});
                var writer = source.CreateWriter();
                await writer.CopyFromAsync(MemorySource.SupplyContent, supplier);
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                var consumer = new ArrayBufferWriter<byte>();
                await reader.CopyToAsync(ConsumeMemory, consumer);
                Equal(supplier.Content, consumer.WrittenMemory.ToArray());
            }

            static ValueTask ConsumeMemory(ReadOnlyMemory<byte> block, IBufferWriter<byte> writer, CancellationToken token)
            {
                writer.Write(block.Span);
                return new ValueTask();
            }
        }

        [Fact]
        public static async Task ReadFromEmptyReader()
        {
            await using var ms = new MemoryStream();
            var reader = IAsyncBinaryReader.Empty;
            await reader.CopyToAsync(ms);
            Equal(0, ms.Length);

            var writer = new ArrayBufferWriter<byte>();
            await reader.CopyToAsync(writer);
            Equal(0, writer.WrittenCount);

            var context = new DecodingContext();
            await ThrowsAsync<EndOfStreamException>(reader.ReadByteAsync(StringLengthEncoding.Plain, context).AsTask);
            await ThrowsAsync<EndOfStreamException>(reader.ReadInt16Async(StringLengthEncoding.Plain, context).AsTask);
            await ThrowsAsync<EndOfStreamException>(reader.ReadInt32Async(StringLengthEncoding.Plain, context).AsTask);
            await ThrowsAsync<EndOfStreamException>(reader.ReadInt64Async(StringLengthEncoding.Plain, context).AsTask);
            await ThrowsAsync<EndOfStreamException>(reader.ReadSingleAsync(StringLengthEncoding.Plain, context).AsTask);
            await ThrowsAsync<EndOfStreamException>(reader.ReadDoubleAsync(StringLengthEncoding.Plain, context).AsTask);
            await ThrowsAsync<EndOfStreamException>(reader.ReadDecimalAsync(StringLengthEncoding.Plain, context).AsTask);
            await ThrowsAsync<EndOfStreamException>(reader.ReadStringAsync(StringLengthEncoding.Plain, context).AsTask);
            await ThrowsAsync<EndOfStreamException>(reader.ReadStringAsync(10, context).AsTask);
            await ThrowsAsync<EndOfStreamException>(reader.ReadAsync<decimal>().AsTask);
            await ThrowsAsync<EndOfStreamException>(reader.ReadAsync(new byte[1]).AsTask);
        }
    }
}