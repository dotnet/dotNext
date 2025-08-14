using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.IO;

using static Buffers.Memory;

internal abstract class SharedReadOnlyMemoryStream(ReadOnlySequence<byte> sequence) : ReadOnlyStream
{
    private protected abstract SequencePosition LocalPosition
    {
        get;
        set;
    }

    private protected SequencePosition StartPosition => sequence.Start;

    private ReadOnlySequence<byte> GetRemainingSequence(out SequencePosition start)
        => sequence.Slice(start = LocalPosition);

    public sealed override bool CanSeek => true;

    public sealed override long Length => sequence.Length;

    public sealed override long Position
    {
        get => sequence.Slice(StartPosition, LocalPosition).Length;
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((ulong)value, (ulong)sequence.Length, nameof(value));

            LocalPosition = sequence.GetPosition(value, StartPosition);
        }
    }

    public sealed override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token)
    {
        ValidateCopyToArguments(destination, bufferSize);

        foreach (var segment in GetRemainingSequence(out _))
            await destination.WriteAsync(segment, token).ConfigureAwait(false);

        LocalPosition = sequence.End;
    }

    public sealed override void CopyTo(Stream destination, int bufferSize)
    {
        ValidateCopyToArguments(destination, bufferSize);

        foreach (var segment in GetRemainingSequence(out _))
            destination.Write(segment.Span);

        LocalPosition = sequence.End;
    }

    public sealed override void SetLength(long value) => throw new NotSupportedException();

    public sealed override int Read(Span<byte> buffer)
    {
        GetRemainingSequence(out var startPos).CopyTo(buffer, out var writtenCount);
        LocalPosition = sequence.GetPosition(writtenCount, startPos);
        return writtenCount;
    }

    public sealed override long Seek(long offset, SeekOrigin origin)
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

        LocalPosition = sequence.GetPosition(newPosition, StartPosition);
        return newPosition;
    }

    public sealed override string ToString() => sequence.ToString();

    internal static SharedReadOnlyMemoryStream CreateAsyncLocalStream(ReadOnlySequence<byte> sequence)
        => new AsyncLocalStream(sequence);

    internal static SharedReadOnlyMemoryStream CreateThreadLocalStream(ReadOnlySequence<byte> sequence)
        => new ThreadLocalStream(sequence);
}

file sealed class AsyncLocalStream(ReadOnlySequence<byte> sequence) : SharedReadOnlyMemoryStream(sequence)
{
    // don't use BoxedValue due to limitations of AsyncLocal
    private readonly AsyncLocal<StrongBox<SequencePosition>> position = new();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private protected override SequencePosition LocalPosition
    {
        get => position.Value?.Value ?? StartPosition;
        set => (position.Value ??= new()).Value = value;
    }
}

file sealed class ThreadLocalStream(ReadOnlySequence<byte> sequence) : SharedReadOnlyMemoryStream(sequence)
{
    private readonly ThreadLocal<SequencePosition> position = new(Func.Constant(sequence.Start), trackAllValues: false);

    private protected override SequencePosition LocalPosition
    {
        get => position.Value;
        set => position.Value = value;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            position.Dispose();
        }

        base.Dispose(disposing);
    }
}