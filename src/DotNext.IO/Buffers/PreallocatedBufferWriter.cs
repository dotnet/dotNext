using System.Buffers;

namespace DotNext.Buffers;

internal sealed class PreallocatedBufferWriter : Disposable, IBufferWriter<byte>
{
    private readonly Memory<byte> buffer;
    private int position;   // written bytes to the pre-allocated buffer
    private PooledBufferWriter<byte>? extraBuffer;

    internal PreallocatedBufferWriter(Memory<byte> buffer)
    {
        this.buffer = buffer;
    }

    internal ReadOnlyMemory<byte> WrittenMemory => extraBuffer is null ? buffer.Slice(0, position) : extraBuffer.WrittenMemory;

    public Memory<byte> GetMemory(int sizeHint)
    {
        if (sizeHint < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeHint));

        var result = buffer.Slice(position);
        if (!result.IsEmpty && result.Length >= sizeHint)
            goto exit;

        if (extraBuffer is null)
        {
            extraBuffer = new() { Capacity = sizeHint + position };
            extraBuffer.Write(buffer.Span);
        }

        result = extraBuffer.GetMemory(sizeHint);

    exit:
        return result;
    }

    public Span<byte> GetSpan(int sizeHint) => GetMemory(sizeHint).Span;

    public void Advance(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (extraBuffer is not null)
        {
            extraBuffer.Advance(count);
        }
        else if (count + position > buffer.Length)
        {
            throw new InvalidOperationException();
        }
        else
        {
            position += count;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            extraBuffer?.Dispose();
            extraBuffer = null;
        }

        base.Dispose(disposing);
    }
}