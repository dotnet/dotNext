using System.Buffers;
using System.Text;
using DotNext.Buffers.Binary;
using DotNext.Runtime.Serialization;

namespace DotNext.IO;

public sealed class DataTransferObjectTests : Test
{
    private sealed class CustomDTO(ReadOnlyMemory<byte> content, bool withLength) : BinaryTransferObject(content), IDataTransferObject
    {
        long? IDataTransferObject.Length => withLength ? Content.Length : null;
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
    public static async Task BufferedDTO()
    {
        var expected = 42L;
        using var dto = new MemoryTransferObject(sizeof(long));
        Span.AsReadOnlyBytes(in expected).CopyTo(dto.Content.Span);
        Equal(sizeof(long), dto.As<IDataTransferObject>().Length);
        True(dto.As<IDataTransferObject>().IsReusable);
        var writer = new ArrayBufferWriter<byte>();
        await dto.WriteToAsync(writer);
        Equal(sizeof(long), writer.WrittenCount);
        Equal(expected, BitConverter.ToInt64(writer.WrittenSpan));
        var memory = await dto.ToByteArrayAsync();
        Equal(expected, BitConverter.ToInt64(memory, 0));
    }

    [Fact]
    public static async Task DecodeAsAllocatedBuffer()
    {
        var expected = 42L;
        using var dto = new MemoryTransferObject(sizeof(long));
        Span.AsReadOnlyBytes(in expected).CopyTo(dto.Content.Span);
        using var memory = await dto.ToMemoryAsync();
        Equal(expected, BitConverter.ToInt64(memory.Span));
    }

    [Fact]
    public static async Task ToBlittableType()
    {
        var expected = 42M;
        var bytes = new byte[sizeof(decimal)];
        Span.AsReadOnlyBytes(in expected).CopyTo(bytes);
        var dto = new BinaryTransferObject(bytes);
        Equal(expected, (await ISerializable<BlittableTransferObject<decimal>>.TransformAsync(dto)).Content);
    }

    [Fact]
    public static async Task DecodeUsingDelegate()
    {
        var dto = new BlittableTransferObject<long> { Content = 42L };
        Equal(42L, (await dto.TransformAsync((reader, token) => reader.ReadAsync<Blittable<long>>(token))).Value);
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

    [Fact]
    public static async Task EmptyObject()
    {
        var empty = IDataTransferObject.Empty;
        Equal(0L, empty.Length);
        True(empty.IsReusable);
        True(empty.TryGetMemory(out var memory));
        True(memory.IsEmpty);

        Empty(await empty.ToByteArrayAsync());

        var writer = new ArrayBufferWriter<byte>();
        True(empty.WriteToAsync(IAsyncBinaryWriter.Create(writer), CancellationToken.None).IsCompletedSuccessfully);
        Equal(0, writer.WrittenCount);
    }
}