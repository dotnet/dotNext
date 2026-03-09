namespace DotNext.IO;

public sealed class StreamSegmentTests : Test
{
    [Theory]
    [InlineData(0, 4, "This")]
    [InlineData(5, 2, "is")]
    [InlineData(10, 4, "test")]
    public static void AdjustSetsSegmentOfStream(int offset, int length, string expected)
    {
        using var ms = new MemoryStream("This is a test"u8.ToArray());
        using var segment = ms.Slice(offset, length);
        using StreamReader reader = new(segment);
        Equal(expected, reader.ReadToEnd());
    }

    [Fact]
    public static void ReadByteSequentially()
    {
        using var ms = new MemoryStream([1, 3, 5, 8, 12]);
        using var segment = new StreamSegment(ms);
        Same(ms, segment.BaseStream);
        Equal(0, segment.Position);
        segment.Range = (0L, 2L);
        Equal(1, segment.ReadByte());
        Equal(1, segment.Position);

        Equal(3, segment.ReadByte());
        Equal(2, segment.Position);

        Equal(-1, segment.ReadByte());
        Equal(2, segment.Position);

        Equal((0L, 2L), segment.Range);
    }

    [Fact]
    public static void SetPosition()
    {
        using var ms = new MemoryStream([1, 3, 5, 8, 12]);
        using var segment = new StreamSegment(ms);
        segment.Range = (1, 3);
        segment.Position = 1;
        Equal(5, segment.ReadByte());
        Equal(2, segment.Position);
        segment.Position = 0;
        Equal(3, segment.ReadByte());
        Equal(1, segment.Position);
    }

    [Fact]
    public static void ReadRange()
    {
        using var ms = new MemoryStream([1, 3, 5, 8, 12]);
        using var segment = new StreamSegment(ms);
        segment.Range = (1L, 2L);
        var buffer = new byte[4];
        Equal(2, segment.Read(buffer, 0, buffer.Length));
        Equal(3, buffer[0]);
        Equal(5, buffer[1]);
        Equal(0, buffer[2]);
        Equal(0, buffer[3]);
        //read from the end of the stream
        Equal(-1, segment.ReadByte());
    }

    [Fact]
    public static async Task ReadRangeAsync()
    {
        using var ms = new MemoryStream([1, 3, 5, 8, 12]);
        using var segment = new StreamSegment(ms);
        segment.Range = (1L, 2L);
        var buffer = new byte[4];
        Equal(2, await segment.ReadAsync(buffer, 0, buffer.Length, TestToken));
        Equal(3, buffer[0]);
        Equal(5, buffer[1]);
        Equal(0, buffer[2]);
        Equal(0, buffer[3]);
        //read from the end of the stream
        Equal(-1, segment.ReadByte());
    }

    [Fact]
    public static void ReadApm()
    {
        using var ms = new MemoryStream([1, 3, 5, 8, 12]);
        using var segment = ms.Slice(1L, 2L);
        segment.Range = (1L, 2L);
        var buffer = new byte[4];
        var ar = segment.BeginRead(buffer, 0, 2, null, null);
        Equal(2, segment.EndRead(ar));
        Equal(3, buffer[0]);
        Equal(5, buffer[1]);
        Equal(0, buffer[2]);
    }

    [Fact]
    public static async Task ExceptionCheck()
    {
        using var ms = new MemoryStream([1, 3, 5, 8, 12]);
        using var segment = new StreamSegment(ms);
        True(segment.CanRead);
        True(segment.CanSeek);
        False(segment.CanWrite);
        Equal(ms.CanTimeout, segment.CanTimeout);
        Throws<ArgumentOutOfRangeException>(() => segment.Range = (ms.Length + 1L, 0L));
        Throws<ArgumentOutOfRangeException>(() => segment.Range = (ms.Length, 1L));
        Throws<ArgumentOutOfRangeException>(() => segment.Range = (ms.Length - 1, 2L));
        Throws<NotSupportedException>(() => segment.WriteByte(2));
        Throws<NotSupportedException>(() => segment.Write(new byte[3], 0, 3));
        Throws<NotSupportedException>(() => segment.Write(new byte[2]));
        Throws<NotSupportedException>(() => segment.BeginWrite(new byte[2], 0, 2, null, null));
        Throws<InvalidOperationException>(() => segment.EndWrite(Task.CompletedTask));
        await ThrowsAsync<NotSupportedException>(() => segment.WriteAsync(new byte[3], 0, 3, TestToken));
        await ThrowsAsync<NotSupportedException>(segment.WriteAsync(ReadOnlyMemory<byte>.Empty, TestToken).AsTask);
    }
}