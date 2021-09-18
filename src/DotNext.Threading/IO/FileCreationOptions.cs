using System.Runtime.InteropServices;

namespace DotNext.IO;

[StructLayout(LayoutKind.Auto)]
internal readonly struct FileCreationOptions
{
    internal readonly FileAccess Access;
    internal readonly FileMode Mode;
    internal readonly FileShare Share;
    internal readonly FileOptions Optimization;
    internal readonly long InitialSize;

    internal FileCreationOptions(FileMode mode, FileAccess access, FileShare share, FileOptions options, long initialSize = 0L)
    {
        Access = access;
        Mode = mode;
        Share = share;
        Optimization = options;
        InitialSize = initialSize;
    }

    internal FileStreamOptions ToFileStreamOptions(int bufferSize) => new()
    {
        Mode = Mode,
        Access = Access,
        Share = Share,
        Options = Optimization,
        PreallocationSize = InitialSize,
    };
}