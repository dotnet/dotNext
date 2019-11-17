using System.IO;
using System.Runtime.InteropServices;

namespace DotNext.IO
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct FileCreationOptions
    {
        internal readonly FileAccess Access;
        internal readonly FileMode Mode;
        internal readonly FileShare Share;

        internal FileCreationOptions(FileMode mode, FileAccess access, FileShare share)
        {
            Access = access;
            Mode = mode;
            Share = share;
        }
    }
}
