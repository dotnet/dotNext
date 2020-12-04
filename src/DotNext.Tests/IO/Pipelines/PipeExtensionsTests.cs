using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.IO.Pipelines
{
    [ExcludeFromCodeCoverage]
    public sealed class PipeExtensionsTests : Test
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task EncodeDecodeValues(bool littleEndian)
        {
            static async void WriteValuesAsync(PipeWriter writer, bool littleEndian)
            {
                await writer.WriteAsync(20M);
                await writer.WriteInt64Async(42L, littleEndian);
                await writer.WriteUInt64Async(43UL, littleEndian);
                await writer.WriteInt32Async(44, littleEndian);
                await writer.WriteUInt32Async(45U, littleEndian);
                await writer.WriteInt16Async(46, littleEndian);
                await writer.WriteUInt16Async(47, littleEndian);
                await writer.CompleteAsync();
            }

            var pipe = new Pipe();
            WriteValuesAsync(pipe.Writer, littleEndian);
            Equal(20M, await pipe.Reader.ReadAsync<decimal>());
            Equal(42L, await pipe.Reader.ReadInt64Async(littleEndian));
            Equal(43UL, await pipe.Reader.ReadUInt64Async(littleEndian));
            Equal(44, await pipe.Reader.ReadInt32Async(littleEndian));
            Equal(45U, await pipe.Reader.ReadUInt32Async(littleEndian));
            Equal(46, await pipe.Reader.ReadInt16Async(littleEndian));
            Equal(47, await pipe.Reader.ReadUInt16Async(littleEndian));
        }

        [Fact]
        [Obsolete("This test is for checking obsolete member")]
        public static async Task EncodeDecodeMemoryObsolete()
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
            await PipeExtensions.ReadAsync(pipe.Reader, portion1);
            await PipeExtensions.ReadAsync(pipe.Reader, portion2);
            Equal(1, portion1[0]);
            Equal(5, portion1[1]);
            Equal(8, portion1[2]);
            Equal(9, portion2[0]);
            Equal(10, portion2[1]);
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
            await pipe.Reader.ReadBlockAsync(portion1);
            await pipe.Reader.ReadBlockAsync(portion2);
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
            await ThrowsAsync<EndOfStreamException>(() => pipe.Reader.ReadBlockAsync(result).AsTask());
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
            Equal(3, await pipe.Reader.CopyToAsync(portion1));
            Equal(2, await pipe.Reader.CopyToAsync(portion2));
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
            Equal(5, await pipe.Reader.CopyToAsync(result));
        }

        [Fact]
        public static async Task EncodeDecodeValue2()
        {
            static async void WriteValueAsync(PipeWriter writer)
            {
                if (BitConverter.IsLittleEndian)
                {
                    await writer.WriteAsync(0L);
                    await writer.WriteAsync(20L);
                }
                else
                {
                    await writer.WriteAsync(20L);
                    await writer.WriteAsync(0L);
                }
                await writer.CompleteAsync();
            }

            var pipe = new Pipe();
            WriteValueAsync(pipe.Writer);
            Equal(20M, await pipe.Reader.ReadAsync<decimal>());
        }

        [Fact]
        public static async Task EndOfStream()
        {
            static async void WriteValueAsync(PipeWriter writer)
            {
                await writer.WriteAsync(0L);
                await writer.CompleteAsync();
            }
            var pipe = new Pipe();
            WriteValueAsync(pipe.Writer);
            await ThrowsAsync<EndOfStreamException>(pipe.Reader.ReadAsync<decimal>().AsTask);
        }

        private static async Task EncodeDecodeStringAsync(Encoding encoding, string value, int bufferSize, StringLengthEncoding? lengthEnc)
        {
            var pipe = new Pipe();
            await pipe.Writer.WriteStringAsync(value.AsMemory(), encoding, bufferSize, lengthEnc);
            var result = await (lengthEnc is null ?
                pipe.Reader.ReadStringAsync(encoding.GetByteCount(value), encoding) :
                pipe.Reader.ReadStringAsync(lengthEnc.Value, encoding));
            Equal(value, result);
        }

        [Theory]
        [InlineData(0, null)]
        [InlineData(10, null)]
        [InlineData(1024, null)]
        [InlineData(0, StringLengthEncoding.Compressed)]
        [InlineData(10, StringLengthEncoding.Compressed)]
        [InlineData(1024, StringLengthEncoding.Compressed)]
        [InlineData(0, StringLengthEncoding.Plain)]
        [InlineData(10, StringLengthEncoding.Plain)]
        [InlineData(1024, StringLengthEncoding.Plain)]
        [InlineData(0, StringLengthEncoding.PlainLittleEndian)]
        [InlineData(10, StringLengthEncoding.PlainLittleEndian)]
        [InlineData(1024, StringLengthEncoding.PlainLittleEndian)]
        [InlineData(0, StringLengthEncoding.PlainBigEndian)]
        [InlineData(10, StringLengthEncoding.PlainBigEndian)]
        [InlineData(1024, StringLengthEncoding.PlainBigEndian)]
        public static async Task EncodeDecodeString(int bufferSize, StringLengthEncoding? lengthEnc)
        {
            const string testString = "abc^$&@^$&@)(_+~";
            await EncodeDecodeStringAsync(Encoding.UTF8, testString, bufferSize, lengthEnc);
            await EncodeDecodeStringAsync(Encoding.Unicode, testString, bufferSize, lengthEnc);
            await EncodeDecodeStringAsync(Encoding.UTF32, testString, bufferSize, lengthEnc);
            await EncodeDecodeStringAsync(Encoding.UTF7, testString, bufferSize, lengthEnc);
            await EncodeDecodeStringAsync(Encoding.ASCII, testString, bufferSize, lengthEnc);
        }

        [Fact]
        public static async Task CopyToBuffer()
        {
            var pipe = new Pipe();
            await pipe.Writer.WriteAsync(new byte[] { 10, 20, 30 });
            await pipe.Writer.CompleteAsync();
            var buffer = new ArrayBufferWriter<byte>();
            Equal(3L, await pipe.Reader.CopyToAsync(buffer));
            Equal(new byte[] { 10, 20, 30 }, buffer.WrittenMemory.ToArray());
        }

        [Fact]
        public static async Task HashEntirePipe()
        {
            byte[] data = { 1, 2, 3, 5, 8, 13 };
            using var alg = new SHA256Managed();
            var hash = new byte[alg.HashSize / 8];
            var pipe = new Pipe();
            ThreadPool.QueueUserWorkItem(async state =>
            {
                await pipe.Writer.WriteAsync(data);
                pipe.Writer.Complete();
            });
            Equal(hash.Length, await pipe.Reader.ComputeHashAsync(HashAlgorithmName.SHA256, hash));
            alg.Initialize();
            Equal(hash, alg.ComputeHash(data));
        }

        [Fact]
        public static async Task HashPipe()
        {
            byte[] data = { 1, 2, 3, 5, 8, 13 };
            using var alg = new SHA256Managed();
            var hash = new byte[alg.HashSize / 8];
            var pipe = new Pipe();
            await pipe.Writer.WriteAsync(data);
            Equal(hash.Length, await pipe.Reader.ComputeHashAsync(HashAlgorithmName.SHA256, data.Length, hash));
            alg.Initialize();
            Equal(hash, alg.ComputeHash(data));
        }
    }
}
