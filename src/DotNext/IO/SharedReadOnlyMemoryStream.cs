using System.Buffers;

namespace DotNext.IO;

using static Buffers.Memory;

internal sealed class SharedReadOnlyMemoryStream(ReadOnlySequence<byte> sequence) : ReadOnlyStream
{
    private readonly AsyncLocal<SequencePosition> position = new();

    private ReadOnlySequence<byte> GetRemainingSequence(out SequencePosition start)
        => sequence.Slice(start = position.Value);

    public override bool CanSeek => true;

    public override long Length => sequence.Length;

    public override long Position
    {
        get => sequence.GetOffset(position.Value);
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((ulong)value, (ulong)sequence.Length, nameof(value));

            position.Value = sequence.GetPosition(value);
        }
    }

    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token)
    {
        ValidateCopyToArguments(destination, bufferSize);

        foreach (var segment in GetRemainingSequence(out _))
            await destination.WriteAsync(segment, token).ConfigureAwait(false);

        position.Value = sequence.End;
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        ValidateCopyToArguments(destination, bufferSize);

        foreach (var segment in GetRemainingSequence(out _))
            destination.Write(segment.Span);

        position.Value = sequence.End;
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override int Read(Span<byte> buffer)
    {
        GetRemainingSequence(out var startPos).CopyTo(buffer, out var writtenCount);
        position.Value = sequence.GetPosition(writtenCount, startPos);
        return writtenCount;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => sequence.GetOffset(position.Value) + offset,
            SeekOrigin.End => sequence.Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (newPosition < 0L)
            throw new IOException();

        ArgumentOutOfRangeException.ThrowIfGreaterThan(newPosition, sequence.Length, nameof(offset));

        position.Value = sequence.GetPosition(newPosition);
        return newPosition;
    }

    public override string ToString() => sequence.ToString();
}