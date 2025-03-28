using Microsoft.Win32.SafeHandles;

namespace DotNext.Runtime.Caching;

using Buffers;

public partial class DiskSpacePool
{
    private readonly SafeFileHandle handle;
    private readonly IReadOnlyList<ReadOnlyMemory<byte>> zeroes;

    private void EraseSegment(long offset)
    {
        switch (zeroes)
        {
            case []:
                break;
            case [var buffer]:
                RandomAccess.Write(handle, buffer.Span, offset);
                break;
            default:
                RandomAccess.Write(handle, zeroes, offset);
                break;
        }
    }

    private void ReleaseSegment(long offset)
    {
        try
        {
            EraseSegment(offset);
        }
        finally
        {
            ReturnOffset(offset);
        }
    }

    private ValueTask EraseSegmentAsync(long offset) => zeroes switch
    {
        [] => ValueTask.CompletedTask,
        [var buffer] => RandomAccess.WriteAsync(handle, buffer, offset),
        _ => RandomAccess.WriteAsync(handle, zeroes, offset),
    };

    private void Write(long absoluteOffset, ReadOnlySpan<byte> buffer, int segmentOffset)
        => RandomAccess.Write(handle, buffer, absoluteOffset + segmentOffset);
    
    private ValueTask WriteAsync(long absoluteOffset, ReadOnlyMemory<byte> buffer, int segmentOffset, CancellationToken token)
        => RandomAccess.WriteAsync(handle, buffer, absoluteOffset + segmentOffset, token);

    private int Read(long absoluteOffset, Span<byte> buffer, int segmentOffset)
        => Read(absoluteOffset, buffer, segmentOffset, MaxSegmentSize - segmentOffset);

    private int Read(long absoluteOffset, Span<byte> buffer, int segmentOffset, int length)
        => RandomAccess.Read(handle, buffer.TrimLength(length), absoluteOffset + segmentOffset);

    private ValueTask<int> ReadAsync(long absoluteOffset, Memory<byte> buffer, int segmentOffset, CancellationToken token)
        => ReadAsync(absoluteOffset, buffer, segmentOffset, MaxSegmentSize - segmentOffset, token);

    private ValueTask<int> ReadAsync(long absoluteOffset, Memory<byte> buffer, int segmentOffset, int length, CancellationToken token)
        => RandomAccess.ReadAsync(handle, buffer.TrimLength(length), absoluteOffset + segmentOffset, token);
}