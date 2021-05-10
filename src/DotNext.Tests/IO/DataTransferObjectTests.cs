using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.IO
{
    [ExcludeFromCodeCoverage]
    public sealed class DataTransferObjectTests : Test
    {
        private sealed class CustomDTO : IDataTransferObject
        {
            private readonly byte[] content;

            internal CustomDTO(byte[] content, bool withLength)
            {
                this.content = content;
                Length = withLength ? content.LongLength : null;
            }

            ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
                => writer.WriteAsync(new ReadOnlyMemory<byte>(content), null, token);

            public bool IsReusable => true;

            public long? Length { get; }
        }

        [Fact]
        public static async Task StreamTransfer()
        {
            const string testString = "abcdef";
            using var ms = new MemoryStream(Encoding.Unicode.GetBytes(testString));
            using var dto = new StreamTransferObject(ms, false);
            Equal(ms.Length, dto.As<IDataTransferObject>().Length);
            Equal(testString, await dto.ToStringAsync(Encoding.Unicode));
        }

        [Fact]
        public static async Task MemoryDTO()
        {
            byte[] content = { 1, 2, 3 };
            IDataTransferObject dto = new BinaryTransferObject(content);
            Equal(3L, dto.Length);
            True(dto.IsReusable);
            using var ms = new MemoryStream();
            await dto.WriteToAsync(ms);
            Equal(3, ms.Length);
            Equal(content, ms.ToArray());
            Equal(content, await dto.ToByteArrayAsync());
        }

        [Fact]
        public static async Task MemoryDTO2()
        {
            byte[] content = { 1, 2, 3 };
            IDataTransferObject dto = new BinaryTransferObject(content);
            Equal(3L, dto.Length);
            True(dto.IsReusable);
            var writer = new ArrayBufferWriter<byte>();
            await dto.WriteToAsync(writer);
            Equal(3, writer.WrittenCount);
            Equal(content, writer.WrittenSpan.ToArray());
        }

        [Fact]
        public static async Task MemoryDTO3()
        {
            IDataTransferObject dto = new BinaryTransferObject<long> { Content = 42L };
            Equal(sizeof(long), dto.Length);
            True(dto.IsReusable);
            var writer = new ArrayBufferWriter<byte>();
            await dto.WriteToAsync(writer);
            Equal(sizeof(long), writer.WrittenCount);
            Equal(42L, BitConverter.ToInt64(writer.WrittenSpan));
            Equal(42L, await dto.ToTypeAsync<long, IDataTransferObject>());
        }

        [Fact]
        public static async Task BufferedDTO()
        {
            using var dto = new MemoryTransferObject(sizeof(long));
            Span.AsReadOnlyBytes(42L).CopyTo(dto.Content.Span);
            Equal(sizeof(long), dto.As<IDataTransferObject>().Length);
            True(dto.As<IDataTransferObject>().IsReusable);
            var writer = new ArrayBufferWriter<byte>();
            await dto.WriteToAsync(writer);
            Equal(sizeof(long), writer.WrittenCount);
            Equal(42L, BitConverter.ToInt64(writer.WrittenSpan));
            var memory = await dto.ToByteArrayAsync();
            Equal(42L, BitConverter.ToInt64(memory, 0));
        }

        [Fact]
        public static async Task DecodeAsAllocatedBuffer()
        {
            using var dto = new MemoryTransferObject(sizeof(long));
            Span.AsReadOnlyBytes(42L).CopyTo(dto.Content.Span);
            using var memory = await dto.ToMemoryAsync();
            Equal(42L, BitConverter.ToInt64(memory.Memory.Span));
        }

        [Fact]
        public static async Task ToBlittableType()
        {
            var bytes = new byte[sizeof(decimal)];
            Span.AsReadOnlyBytes(42M).CopyTo(bytes);
            var dto = new BinaryTransferObject(bytes);
            Equal(42M, await dto.ToTypeAsync<decimal, BinaryTransferObject>());
        }

        [Fact]
        public static async Task DecodeUsingDelegate()
        {
            var dto = new BinaryTransferObject<long> { Content = 42L };
            Equal(42L, await dto.TransformAsync((reader, token) => reader.ReadAsync<long>(token)));
        }

        [Theory]
        [InlineData(128, false)]
        [InlineData(128, true)]
        [InlineData(ushort.MaxValue, true)]
        [InlineData(ushort.MaxValue, false)]
        public static async Task DefaultDecodeAsync(int dataSize, bool withLength)
        {
            var data = RandomBytes(dataSize);
            IDataTransferObject dto = new CustomDTO(data, withLength);
            True(dto.IsReusable);
            True(withLength == dto.Length.HasValue);
            Equal(data, await dto.ToByteArrayAsync());
        }
    }
}
