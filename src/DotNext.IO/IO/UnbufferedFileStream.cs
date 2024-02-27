using System.Runtime.CompilerServices;
using Microsoft.Win32.SafeHandles;

namespace DotNext.IO;

internal sealed partial class UnbufferedFileStream(SafeFileHandle handle, FileAccess access) : Stream, IFlushable
{
    private static readonly Action<SafeFileHandle> FlushToDiskAction = RandomAccess.FlushToDisk;
    private long position;

    public override bool CanRead => access.HasFlag(FileAccess.Read);

    public override bool CanWrite => access.HasFlag(FileAccess.Write);

    public override bool CanSeek { get; } = CheckSeekable(handle);

    private static bool CheckSeekable(SafeFileHandle handle)
    {
        bool result;
        try
        {
            result = CheckCanSeekImpl(handle);
        }
        catch (MissingMethodException)
        {
            result = false;
        }

        return result;

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_CanSeek")]
        static extern bool CheckCanSeekImpl(SafeFileHandle handle);
    }

    public override long Length => RandomAccess.GetLength(handle);

    public override long Position
    {
        get => position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(position);

            position = value;
        }
    }

    public override void Flush() => RandomAccess.FlushToDisk(handle);

    public override Task FlushAsync(CancellationToken token)
        => Task.Run(FlushToDiskAction.Bind(handle), token);

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);

        return Read(new Span<byte>(buffer, offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        var bytesRead = RandomAccess.Read(handle, buffer, position);
        Advance(bytesRead);
        return bytesRead;
    }

    public override int ReadByte()
    {
        Unsafe.SkipInit(out byte result);

        return Read(new Span<byte>(ref result)) is not 0 ? result : -1;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
        => SubmitRead(RandomAccess.ReadAsync(handle, buffer, position, token));

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
    {
        ValidateBufferArguments(buffer, offset, count);

        var bytesRead = await RandomAccess.ReadAsync(handle, buffer.AsMemory(offset, count), position, token).ConfigureAwait(false);
        Advance(bytesRead);
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };

        return position = newPosition >= 0L
            ? newPosition
            : throw new IOException();
    }

    public override void SetLength(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        RandomAccess.SetLength(handle, value);

        if (position > value)
            position = value;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);

        Write(new ReadOnlySpan<byte>(buffer, offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        RandomAccess.Write(handle, buffer, position);
        Advance(buffer.Length);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
        => SubmitWrite(RandomAccess.WriteAsync(handle, buffer, position, token), buffer.Length);

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
    {
        ValidateBufferArguments(buffer, offset, count);

        await RandomAccess.WriteAsync(handle, new ReadOnlyMemory<byte>(buffer, offset, count), position, token).ConfigureAwait(false);
        Advance(count);
    }

    public override void WriteByte(byte value)
        => Write(new ReadOnlySpan<byte>(ref value));

    private void Advance(int count) => position += count;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            readCallback = writeCallback = null; // help GC
            readTask = default;
            writeTask = default;
            source = default;
        }

        base.Dispose(disposing);
    }
}