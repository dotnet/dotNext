using System.Text;

namespace DotNext.Runtime.Caching;

using IO;

public sealed class DiskSpacePoolTests : Test
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task RentConcurrently(bool optimizedDiskAllocation)
    {
        using var pool = new DiskSpacePool(maxSegmentSize: 1028 * 1024,
            new() { IsAsynchronous = true, OptimizedDiskAllocation = optimizedDiskAllocation, ExpectedNumberOfSegments = 3 });

        var rent = pool.Rent;
        var t1 = Task.Run(rent);
        var t2 = Task.Run(rent);
        var t3 = Task.Run(rent);

        var segments =  await Task.WhenAll(t1, t2, t3);

        var last = segments.Last();
        ReadOnlyMemory<byte> expected = RandomBytes(pool.MaxSegmentSize);
        await last.WriteAsync(expected);

        var actual = new byte[expected.Length];
        Equal(actual.Length, await last.ReadAsync(actual));
        Equal(expected, actual);

        Disposable.Dispose(segments);
    }

    [Fact]
    public static void RentRelease()
    {
        using var pool = new DiskSpacePool(maxSegmentSize: 1028 * 1024);

        pool.Rent().Dispose();
        pool.Rent().Dispose();
    }

    [Fact]
    public static void ReadWrite()
    {
        using var pool = new DiskSpacePool(maxSegmentSize: 1028 * 1024);

        using var segment = pool.Rent();
        ReadOnlySpan<byte> expected = RandomBytes(pool.MaxSegmentSize);
        segment.Write(expected);

        Span<byte> actual = new byte[expected.Length];
        Equal(actual.Length, segment.Read(actual));

        Equal(expected, actual);
    }
    
    [Fact]
    public static async Task ReadWriteAsync()
    {
        using var pool = new DiskSpacePool(maxSegmentSize: 1028 * 1024);

        await using var segment = pool.Rent();
        ReadOnlyMemory<byte> expected = RandomBytes(pool.MaxSegmentSize);
        await segment.WriteAsync(expected);

        Memory<byte> actual = new byte[expected.Length];
        Equal(actual.Length, await segment.ReadAsync(actual));

        Equal(expected, actual);
    }

    [Fact]
    public static void OverflowOnWrite()
    {
        using var pool = new DiskSpacePool(maxSegmentSize: 1028 * 1024);

        using var segment = pool.Rent();
        byte[] expected = RandomBytes(pool.MaxSegmentSize + 1);
        Throws<ArgumentOutOfRangeException>(() => segment.Write(expected));
    }
    
    [Fact]
    public static async Task OverflowOnWriteAsync()
    {
        using var pool = new DiskSpacePool(maxSegmentSize: 1028 * 1024);

        await using var segment = pool.Rent();
        ReadOnlyMemory<byte> expected = RandomBytes(pool.MaxSegmentSize + 1);
        await ThrowsAsync<ArgumentOutOfRangeException>(segment.WriteAsync(expected).AsTask);
    }
    
    [Fact]
    public static void ReadWriteString()
    {
        const string expected = "Hello, world!";
        using var pool = new DiskSpacePool(maxSegmentSize: 1028 * 1024);

        using var segment = pool.Rent();
        using var stream = segment.CreateStream();
        Equal(pool.MaxSegmentSize, stream.Length);
        stream.SetLength(0L);

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("Hello, world!");
            writer.Flush();
        }

        stream.Seek(0L, SeekOrigin.Begin);

        using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
        {
            Equal(expected, reader.ReadString());
        }
        
        // check EOS
        Equal(0, stream.Read(stackalloc byte[1]));
    }

    [Fact]
    public static async Task ReadWriteStringAsync()
    {
        const string expected = "Hello, world!";
        using var pool = new DiskSpacePool(maxSegmentSize: 1028 * 1024);

        await using var segment = pool.Rent();
        await using var stream = segment.CreateStream();
        Equal(pool.MaxSegmentSize, stream.Length);
        stream.SetLength(0L);
        
        var buffer = new byte[32];

        await stream.EncodeAsync(expected.AsMemory(), Encoding.UTF8, LengthFormat.LittleEndian, buffer);
        
        stream.Seek(0L, SeekOrigin.Begin);

        using var actual = await stream.DecodeAsync(Encoding.UTF8, LengthFormat.LittleEndian, buffer);
        Equal(expected, actual.Span);

        // check EOS
        Equal(0, await stream.ReadAsync(buffer));
    }
}