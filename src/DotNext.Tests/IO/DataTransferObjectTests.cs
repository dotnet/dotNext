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
        }
    }
}
