using Microsoft.Win32.SafeHandles;

namespace DotNext.Runtime.Caching;

using Buffers;

public partial class DiskSpacePool
{
    private readonly SafeFileHandle handle;
    private readonly ReadOnlyMemory<byte> zeroes;
    
    private void EraseSegment(long offset)
    {
        if (zeroes.Span is { IsEmpty: false } span)
        {
            RandomAccess.Write(handle, span, offset);
        }
    }

    private void ReleaseSegment(long offset)
    {
        EraseSegment(offset);
        ReturnOffset(offset);
    }

    private ValueTask EraseSegmentAsync(long offset)
        => zeroes.IsEmpty ? ValueTask.CompletedTask : RandomAccess.WriteAsync(handle, zeroes, offset);
    
    private async ValueTask ReleaseSegmentAsync(long offset)
    {
        await EraseSegmentAsync(offset).ConfigureAwait(false);
        ReturnOffset(offset);
    }

    private void Write(long absoluteOffset, ReadOnlySpan<byte> buffer, int segmentOffset)
        => RandomAccess.Write(handle, buffer, absoluteOffset + segmentOffset);
    
    private ValueTask WriteAsync(long absoluteOffset, ReadOnlyMemory<byte> buffer, int segmentOffset, CancellationToken token)
        => RandomAccess.WriteAsync(handle, buffer, absoluteOffset + segmentOffset, token);

    private int Read(long absoluteOffset, Span<byte> buffer, int segmentOffset)
    {
        buffer = buffer.TrimLength(MaxSegmentSize - segmentOffset);
        return RandomAccess.Read(handle, buffer, absoluteOffset + segmentOffset);
    }

    private ValueTask<int> ReadAsync(long absoluteOffset, Memory<byte> buffer, int segmentOffset, CancellationToken token)
    {
        buffer = buffer.TrimLength(MaxSegmentSize - segmentOffset);
        return RandomAccess.ReadAsync(handle, buffer, absoluteOffset + segmentOffset, token);
    }
}