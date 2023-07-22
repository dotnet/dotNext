using System.Buffers;
using System.IO.MemoryMappedFiles;

namespace DotNext.IO.MemoryMappedFiles;

public sealed class ReadOnlySequenceAccessorTests : Test
{
    [Fact]
    public static void IteratingOverSegments()
    {
        var tempFile = Path.GetTempFileName();
        var content = RandomBytes(1024);
        using (var fs = new FileStream(tempFile, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            fs.Write(content);
            fs.Flush();
        }

        using var mappedFile = MemoryMappedFile.CreateFromFile(tempFile, FileMode.Open, null, content.Length, MemoryMappedFileAccess.Read);
        using var accessor = new ReadOnlySequenceAccessor(mappedFile, 129, content.Length);
        var sequence = accessor.Sequence;
        Equal(content.Length, sequence.Length);
        False(sequence.IsSingleSegment);

        var offset = 0;
        for (var position = sequence.Start; sequence.TryGet(ref position, out var block) && !block.IsEmpty; offset += block.Length)
        {
            True(block.Length <= 129);
            True(new ReadOnlySpan<byte>(content, offset, block.Length).SequenceEqual(block.Span));
        }
    }

    [Fact]
    public static void ContentEquality()
    {
        var tempFile = Path.GetTempFileName();
        var content = new byte[1024];
        Random.Shared.NextBytes(content);
        using (var fs = new FileStream(tempFile, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            fs.Write(content);
            fs.Flush();
        }

        using var mappedFile = MemoryMappedFile.CreateFromFile(tempFile, FileMode.Open, null, content.Length, MemoryMappedFileAccess.Read);
        using var accessor = new ReadOnlySequenceAccessor(mappedFile, 129, content.Length);
        var sequence = accessor.Sequence;
        Equal(content.Length, sequence.Length);
        False(sequence.IsSingleSegment);
        Equal(content, sequence.ToArray());
    }
}