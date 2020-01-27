using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.IO
{
    [ExcludeFromCodeCoverage]
    public sealed class DataTransferObjectTests : Assert
    {
        [Fact]
        public static async Task StreamTransfer()
        {
            const string testString = "abcdef";
            using var ms = new MemoryStream(Encoding.Unicode.GetBytes(testString));
            using var dto = new StreamTransferObject(ms, false);
            Equal(ms.Length, ((IDataTransferObject)dto).Length);
            Equal(testString, await dto.ToStringAsync(Encoding.Unicode));
            ms.Position = 0;
            Equal(testString, await dto.ToStringAsync(Encoding.Unicode, 1024));
        }

        [Fact]
        public static async Task MemoryDTO()
        {
            IDataTransferObject dto = new BinaryTransferObject(new byte[] { 1, 2, 3 });
            Equal(3L, dto.Length);
            True(dto.IsReusable);
            using var ms = new MemoryStream();
            await dto.WriteToAsync(ms);
            Equal(3, ms.Length);
            var bytes = await dto.ToByteArrayAsync();
            Equal(1, bytes[0]);
            Equal(2, bytes[1]);
            Equal(3, bytes[2]);
        }

        [Fact]
        public static async Task ToBlittableType()
        {
            var bytes = new byte[sizeof(decimal)];
            Span.AsReadOnlyBytes(42M).CopyTo(bytes);
            var dto = new BinaryTransferObject(bytes);
            Equal(42M, await dto.ToType<decimal, BinaryTransferObject>());
        }
    }
}
