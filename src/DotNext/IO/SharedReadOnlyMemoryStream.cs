using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.IO;

using static Buffers.Memory;

internal sealed class SharedReadOnlyMemoryStream(ReadOnlySequence<byte> sequence) : ReadOnlyStream
{
    // don't use BoxedValue due to limitations of AsyncLocal
    private readonly AsyncLocal<StrongBox<SequencePosition>> position = new();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private SequencePosition LocalPosition
    {
        get => position.Value?.Value ?? sequence.Start;
        set => (position.Value ??= new()).Value = value;
    }

    private ReadOnlySequence<byte> GetRemainingSequence(out SequencePosition start)
        => sequence.Slice(start = LocalPosition);

    public override bool CanSeek => true;

    public override long Length => sequence.Length;

    public override long Position
    {
        get => sequence.GetOffset(LocalPosition);
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((ulong)value, (ulong)sequence.Length, nameof(value));

            LocalPosition = sequence.GetPosition(value);
        }
    }

    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token)
    {
        ValidateCopyToArguments(destination, bufferSize);

        foreach (var segment in GetRemainingSequence(out _))
            await destination.WriteAsync(segment, token).ConfigureAwait(false);

        LocalPosition = sequence.End;
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        ValidateCopyToArguments(destination, bufferSize);

        foreach (var segment in GetRemainingSequence(out _))
            destination.Write(segment.Span);

        LocalPosition = sequence.End;
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override int Read(Span<byte> buffer)
    {
        GetRemainingSequence(out var startPos).CopyTo(buffer, out var writtenCount);
        LocalPosition = sequence.GetPosition(writtenCount, startPos);
        return writtenCount;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => sequence.GetOffset(LocalPosition) + offset,
            SeekOrigin.End => sequence.Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (newPosition < 0L)
            throw new IOException();

        ArgumentOutOfRangeException.ThrowIfGreaterThan(newPosition, sequence.Length, nameof(offset));

        LocalPosition = sequence.GetPosition(newPosition);
        return newPosition;
    }

    public override string ToString() => sequence.ToString();
}