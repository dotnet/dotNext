using System.Runtime.CompilerServices;
using Microsoft.Win32.SafeHandles;

namespace DotNext.IO;

internal sealed class UnbufferedFileStream(SafeFileHandle handle, FileAccess access) : RandomAccessStream
{
    private static readonly Action<SafeFileHandle> FlushToDiskAction = RandomAccess.FlushToDisk;

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

    public override void Flush() => RandomAccess.FlushToDisk(handle);

    public override Task FlushAsync(CancellationToken token)
        => Task.Run(FlushToDiskAction.Bind(handle), token);
    
    public override void SetLength(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        RandomAccess.SetLength(handle, value);
        Position = long.Clamp(Position, 0L, value);
    }

    protected override void Write(ReadOnlySpan<byte> buffer, long offset)
        => RandomAccess.Write(handle, buffer, offset);

    protected override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, long offset, CancellationToken token)
        => RandomAccess.WriteAsync(handle, buffer, offset, token);

    protected override int Read(Span<byte> buffer, long offset)
        => RandomAccess.Read(handle, buffer, offset);

    protected override ValueTask<int> ReadAsync(Memory<byte> buffer, long offset, CancellationToken token)
        => RandomAccess.ReadAsync(handle, buffer, offset, token);
}