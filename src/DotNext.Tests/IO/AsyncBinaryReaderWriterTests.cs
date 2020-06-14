using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.IO
{
    using ChunkSequence = Buffers.ChunkSequence<byte>;

    public sealed class AsyncBinaryReaderWriterTests : Test
    {
        public interface IAsyncBinaryReaderWriterSource : IAsyncDisposable
        {
            IAsyncBinaryWriter CreateWriter();

            IAsyncBinaryReader CreateReader();
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
            yield return new object[] { new StreamSource(), true };
            yield return new object[] { new StreamSource(), false };
            yield return new object[] { new PipeSource(), true };
            yield return new object[] { new PipeSource(), false };
            yield return new object[] { new PipeSourceWithSettings(), true };
            yield return new object[] { new PipeSourceWithSettings(), false };
            yield return new object[] { new ReadOnlySequenceSource(), true };
            yield return new object[] { new ReadOnlySequenceSource(), false };
        }

        [Theory]
        [MemberData(nameof(GetDataForPrimitives))]
        public static async Task WriteReadPrimitivesAsync(IAsyncBinaryReaderWriterSource source, bool littleEndian)
        {
            await using (source)
            {
                const short value16 = 42;
                const int value32 = int.MaxValue;
                const long value64 = long.MaxValue;
                const decimal valueM = 42M;

                var writer = source.CreateWriter();
                await writer.WriteInt16Async(value16, littleEndian);
                await writer.WriteInt32Async(value32, littleEndian);
                await writer.WriteInt64Async(value64, littleEndian);
                await writer.WriteAsync(valueM);

                var reader = source.CreateReader();
                Equal(value16, await reader.ReadInt16Async(littleEndian));
                Equal(value32, await reader.ReadInt32Async(littleEndian));
                Equal(value64, await reader.ReadInt64Async(littleEndian));
                Equal(valueM, await reader.ReadAsync<decimal>());
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