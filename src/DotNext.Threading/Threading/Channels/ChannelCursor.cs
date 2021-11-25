using System.Runtime.InteropServices;
using BinaryPrimitives = System.Buffers.Binary.BinaryPrimitives;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.Threading.Channels;

/*
 * State file format:
 * [8 bytes] = message number
 * [8 bytes] = offset in stream
 */
[StructLayout(LayoutKind.Auto)]
internal struct ChannelCursor
{
    private const long StateFileSize = sizeof(long) + sizeof(long);
    private const int PositionOffset = 0;
    private const int OffsetOffset = PositionOffset + sizeof(long);
    private readonly SafeFileHandle stateFile;
    private readonly byte[] stateBuffer;
    private long position;
    private long offset;

    internal ChannelCursor(DirectoryInfo location, string stateFileName)
    {
        stateFileName = Path.Combine(location.FullName, stateFileName);
        stateBuffer = new byte[StateFileSize];
        if (File.Exists(stateFileName))
        {
            stateFile = File.OpenHandle(stateFileName, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.None);
            RandomAccess.Read(stateFile, stateBuffer, 0L);
        }
        else
        {
            // open handle in synchronous mode to allocate space on the disk
            stateFile = File.OpenHandle(stateFileName, FileMode.CreateNew, FileAccess.Write, FileShare.None, FileOptions.None, StateFileSize);
            RandomAccess.Write(stateFile, stateBuffer, 0L);
        }

        stateFile.Dispose();
        stateFile = File.OpenHandle(stateFileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, FileOptions.Asynchronous);
        position = BinaryPrimitives.ReadInt64LittleEndian(stateBuffer.AsSpan(PositionOffset));
        offset = BinaryPrimitives.ReadInt64LittleEndian(stateBuffer.AsSpan(OffsetOffset));
    }

    internal readonly long Position => position;

    internal readonly void Adjust(Stream stream) => stream.Position = offset;

    internal void Reset()
        => BinaryPrimitives.WriteInt64LittleEndian(stateBuffer.AsSpan(OffsetOffset), offset = 0L);

    internal ValueTask AdvanceAsync(long offset, CancellationToken token)
    {
        BinaryPrimitives.WriteInt64LittleEndian(stateBuffer.AsSpan(PositionOffset), position += 1L);
        BinaryPrimitives.WriteInt64LittleEndian(stateBuffer.AsSpan(OffsetOffset), this.offset = offset);
        return RandomAccess.WriteAsync(stateFile, stateBuffer, 0L, token);
    }

    public void Dispose()
    {
        stateFile?.Dispose();
        this = default;
    }
}