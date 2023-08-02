namespace DotNext.IO;

public sealed class StreamSegmentTests : Test
{
    [Fact]
    public static void ReadByteSequentially()
    {
        using var ms = new MemoryStream(new byte[] { 1, 3, 5, 8, 12 });
        using var segment = new StreamSegment(ms);
        Equal(0, segment.Position);
        segment.Adjust(0, 2);
        Equal(1, segment.ReadByte());
        Equal(1, segment.Position);

        Equal(3, segment.ReadByte());
        Equal(2, segment.Position);

        Equal(-1, segment.ReadByte());
        Equal(2, segment.Position);
    }

    [Fact]
    public static void SetPosition()
    {
        using var ms = new MemoryStream(new byte[] { 1, 3, 5, 8, 12 });
        using var segment = new StreamSegment(ms);
        segment.Adjust(1, 3);
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
        using var ms = new MemoryStream(new byte[] { 1, 3, 5, 8, 12 });
        using var segment = new StreamSegment(ms);
        segment.Adjust(1L, 2L);
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
        using var ms = new MemoryStream(new byte[] { 1, 3, 5, 8, 12 });
        using var segment = new StreamSegment(ms);
        segment.Adjust(1L, 2L);
        var buffer = new byte[4];
        Equal(2, await segment.ReadAsync(buffer, 0, buffer.Length));
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
        using var ms = new MemoryStream(new byte[] { 1, 3, 5, 8, 12 });
        using var segment = new StreamSegment(ms);
        segment.Adjust(1L, 2L);
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
        using var ms = new MemoryStream(new byte[] { 1, 3, 5, 8, 12 });
        using var segment = new StreamSegment(ms);
        True(segment.CanRead);
        True(segment.CanSeek);
        False(segment.CanWrite);
        Equal(ms.CanTimeout, segment.CanTimeout);
        Throws<NotSupportedException>(() => segment.WriteByte(2));
        Throws<NotSupportedException>(() => segment.Write(new byte[3], 0, 3));
        Throws<NotSupportedException>(() => segment.Write(new byte[2]));
        Throws<NotSupportedException>(() => segment.BeginWrite(new byte[2], 0, 2, null, null));
        Throws<InvalidOperationException>(() => segment.EndWrite(Task.CompletedTask));
        await ThrowsAsync<NotSupportedException>(() => segment.WriteAsync(new byte[3], 0, 3));
        await ThrowsAsync<NotSupportedException>(segment.WriteAsync(new ReadOnlyMemory<byte>()).AsTask);
    }
}