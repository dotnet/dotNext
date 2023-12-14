using System.Buffers;

namespace DotNext.IO;

using static Buffers.Memory;

internal sealed class ReadOnlyMemoryStream : ReadOnlyStream
{
    private ReadOnlySequence<byte> sequence;
    private SequencePosition position;

    internal ReadOnlyMemoryStream(ReadOnlySequence<byte> sequence)
    {
        this.sequence = sequence;
        position = sequence.Start;
    }

    private ReadOnlySequence<byte> RemainingSequence => sequence.Slice(position);

    public override bool CanSeek => true;

    public override long Length => sequence.Length;

    public override long Position
    {
        get => sequence.GetOffset(position);
        set
        {
            if ((ulong)value > (ulong)sequence.Length)
                throw new ArgumentOutOfRangeException(nameof(value));

            position = sequence.GetPosition(value);
        }
    }

    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token)
    {
        ValidateCopyToArguments(destination, bufferSize);

        foreach (var segment in RemainingSequence)
            await destination.WriteAsync(segment, token).ConfigureAwait(false);

        position = sequence.End;
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        ValidateCopyToArguments(destination, bufferSize);

        foreach (var segment in RemainingSequence)
            destination.Write(segment.Span);

        position = sequence.End;
    }

    public override void SetLength(long value)
    {
        var newSeq = sequence.Slice(0L, value);
        position = newSeq.GetPosition(Math.Min(sequence.GetOffset(position), newSeq.Length));
        sequence = newSeq;
    }

    public override int Read(Span<byte> buffer)
    {
        RemainingSequence.CopyTo(buffer, out var writtenCount);
        position = sequence.GetPosition(writtenCount, position);
        return writtenCount;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => sequence.GetOffset(position) + offset,
            SeekOrigin.End => sequence.Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (newPosition < 0L)
            throw new IOException();

        if (newPosition > sequence.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        position = sequence.GetPosition(newPosition);
        return newPosition;
    }

    public override string ToString() => sequence.ToString();
}