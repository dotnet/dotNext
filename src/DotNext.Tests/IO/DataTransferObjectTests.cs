using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.IO
{
    [ExcludeFromCodeCoverage]
    public sealed class DataTransferObjectTests : Test
    {
        [Fact]
        public static async Task StreamTransfer()
        {
            const string testString = "abcdef";
            using var ms = new MemoryStream(Encoding.Unicode.GetBytes(testString));
            using var dto = new StreamTransferObject(ms, false);
            Equal(ms.Length, dto.As<IDataTransferObject>().Length);
            Equal(testString, await dto.ToStringAsync(Encoding.Unicode));
            ms.Position = 0;
            Equal(testString, await dto.ToStringAsync(Encoding.Unicode, 1024));
        }

        [Fact]
        public static async Task MemoryDTO()
        {
            byte[] content = {1, 2, 3};
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
        }

        [Fact]
        public static async Task ToBlittableType()
        {
            var bytes = new byte[sizeof(decimal)];
            Span.AsReadOnlyBytes(42M).CopyTo(bytes);
            var dto = new BinaryTransferObject(bytes);
            Equal(42M, await dto.ToType<decimal, BinaryTransferObject>());
        }

        [Fact]
        public static async Task DecodeUsingDelegate()
        {
            var dto = new BinaryTransferObject<long> { Content = 42L };
            Equal(42L, await dto.GetObjectDataAsync((reader, token) => reader.ReadAsync<long>(token)));
        }
    }
}
