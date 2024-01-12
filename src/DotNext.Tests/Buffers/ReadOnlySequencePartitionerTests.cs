namespace DotNext.Buffers;

public class ReadOnlySequencePartitionerTests : Test
{
    [Theory]
    [InlineData(false, 64)]
    [InlineData(true, 64)]
    [InlineData(false, 77)]
    [InlineData(true, 77)]
    public static void ParallelProcessing(bool splitOnSegments, int chunkSize)
    {
        var values = new int[1025];
        values.AsSpan().ForEach(static (ref int element, int index) => element = index);

        var partitioner = ToReadOnlySequence<int>(values, chunkSize).CreatePartitioner(splitOnSegments);

        Equal(524_800L, partitioner.AsParallel().Aggregate(0, static (x, y) => x + y));
    }
}