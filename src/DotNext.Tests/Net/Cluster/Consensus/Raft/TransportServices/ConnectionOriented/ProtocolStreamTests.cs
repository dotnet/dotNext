namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;

public sealed class ProtocolStreamTests : Test
{
    [Theory]
    [InlineData(32)]
    [InlineData(128)]
    [InlineData(1024)]
    public static void CopyFrames(int bufferSize)
    {
        using var source = new MemoryStream(512);
        var expected = RandomBytes(512);
        using var protocol = new ProtocolStream(source, MemoryAllocator.CreateArrayAllocator<byte>(), 17);
        protocol.PrepareForWrite();
        protocol.Write(expected);
        protocol.Flush();

        using var destination = new MemoryStream(512);
        protocol.Position = 0L;
        protocol.Reset();
        protocol.CopyTo(destination, bufferSize);
        Equal(expected, destination.ToArray());
    }

    [Theory]
    [InlineData(32)]
    [InlineData(128)]
    [InlineData(1024)]
    public static async Task CopyFramesAsync(int bufferSize)
    {
        using var source = new MemoryStream(512);
        var expected = RandomBytes(512);
        using var protocol = new ProtocolStream(source, MemoryAllocator.CreateArrayAllocator<byte>(), 17);
        protocol.PrepareForWrite();
        await protocol.WriteAsync(expected);
        await protocol.FlushAsync();

        using var destination = new MemoryStream(512);
        protocol.Position = 0L;
        protocol.Reset();
        await protocol.CopyToAsync(destination, bufferSize);
        Equal(expected, destination.ToArray());
    }
}