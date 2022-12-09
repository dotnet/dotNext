using System.Runtime.InteropServices;

namespace DotNext.IO;

[StructLayout(LayoutKind.Auto)]
internal readonly struct FileStreamFactory
{
    internal FileAccess Access { get; init; }

    internal FileMode Mode { get; init; }

    internal FileShare Share { get; init; }

    internal FileOptions Optimization { get; init; }

    internal long InitialSize { get; init; }

    internal FileStream CreateStream(string fileName, int bufferSize)
        => new(fileName, Mode, Access, Share, bufferSize, Optimization);
}