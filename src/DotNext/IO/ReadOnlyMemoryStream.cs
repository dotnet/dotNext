using System.Buffers;

namespace DotNext.IO;

using static Buffers.Memory;

internal sealed class ReadOnlyMemoryStream(ReadOnlySequence<byte> sequence) : ReadOnlyStream
{
    private SequencePosition position = sequence.Start;

    private ReadOnlySequence<byte> RemainingSequence => sequence.Slice(position);

    public override bool CanSeek => true;

    public override long Length => sequence.Length;

    public override long Position
    {
        get => sequence.Slice(sequence.Start, position).Length;
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((ulong)value, (ulong)sequence.Length, nameof(value));

            position = sequence.GetPosition(value, sequence.Start);
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
        var newSeq = sequence.Slice(sequence.Start, value);
        position = newSeq.GetPosition(Math.Min(Position, newSeq.Length));
        sequence = newSeq;
    }

    public override int Read(Span<byte> buffer)
    {
        RemainingSequence.CopyTo(buffer, out int writtenCount);
        position = sequence.GetPosition(writtenCount, position);
        return writtenCount;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => sequence.Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (newPosition < 0L)
            throw new IOException();

        ArgumentOutOfRangeException.ThrowIfGreaterThan(newPosition, sequence.Length, nameof(offset));

        position = sequence.GetPosition(newPosition, sequence.Start);
        return newPosition;
    }

    public override string ToString() => sequence.ToString();
}