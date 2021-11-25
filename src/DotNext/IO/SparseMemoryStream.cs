namespace DotNext.IO;

using SparseBufferWriter = Buffers.SparseBufferWriter<byte>;

internal sealed class SparseMemoryStream : ReadOnlyStream
{
    private SparseBufferWriter.MemoryChunk? current;
    private long position;
    private int offset; // offset within the current chunk

    internal SparseMemoryStream(SparseBufferWriter writer)
    {
        current = writer.FirstChunk;
        Length = writer.WrittenCount;
    }

    public override long Length { get; }

    public override bool CanSeek => false;

    public override long Position
    {
        get => position;
        set => throw new NotSupportedException();
    }

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override int Read(Span<byte> output)
    {
        if (output.IsEmpty || current is null)
            return 0;

        var currentBlock = current.WrittenMemory.Span;
        currentBlock.Slice(offset).CopyTo(output, out var writtenCount);
        offset += writtenCount;
        position += writtenCount;
        if (offset == currentBlock.Length)
        {
            offset = 0;
            current = current.Next;
        }

        return writtenCount;
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        ValidateCopyToArguments(destination, bufferSize);

        for (ReadOnlySpan<byte> currentBlock; current is not null; offset = 0, current = current.Next, position += currentBlock.Length)
        {
            destination.Write(currentBlock = current.WrittenMemory.Span.Slice(offset));
        }
    }

    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token)
    {
        ValidateCopyToArguments(destination, bufferSize);

        for (ReadOnlyMemory<byte> currentBlock; current is not null; offset = 0, current = current.Next, position += currentBlock.Length)
        {
            await destination.WriteAsync(currentBlock = current.WrittenMemory.Slice(offset), token).ConfigureAwait(false);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            current = null;
        }

        offset = 0;
        position = 0L;
        base.Dispose(disposing);
    }
}