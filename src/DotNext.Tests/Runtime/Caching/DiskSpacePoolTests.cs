using System.IO.MemoryMappedFiles;

namespace DotNext.Runtime.Caching;

public sealed class DiskSpacePoolTests : Test
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task RentConcurrently(bool dontCleanUpDiskSpace)
    {
        using var pool = new DiskSpacePool(maxSegmentSize: 1028 * 1024, new() { IsAsynchronous = true, DontCleanDiskSpace = dontCleanUpDiskSpace });

        var t1 = Task.Run(RentSegment);
        var t2 = Task.Run(RentSegment);
        var t3 = Task.Run(RentSegment);

        var segments =  await Task.WhenAll(t1, t2, t3);

        var last = segments.Last();
        ReadOnlyMemory<byte> expected = RandomBytes(pool.MaxSegmentSize);
        await last.WriteAsync(expected);

        var actual = new byte[expected.Length];
        Equal(actual.Length, await last.ReadAsync(actual));
        Equal(expected, actual);

        Disposable.Dispose(segments);

        DiskSpacePool.Segment RentSegment() => pool.Rent();
    }

    [Fact]
    public static void RentRelease()
    {
        using var pool = new DiskSpacePool(maxSegmentSize: 1028 * 1024);

        pool.Rent().Dispose();
    }
}