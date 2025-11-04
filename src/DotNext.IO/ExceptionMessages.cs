using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;

namespace DotNext;

using static Resources.ResourceManagerExtensions;

[ExcludeFromCodeCoverage]
internal static class ExceptionMessages
{
    private static readonly ResourceManager Resources = new("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

    internal static string BufferTooSmall => (string)Resources.Get();

    internal static string StreamNotWritable => (string)Resources.Get();

    internal static string StreamNotReadable => (string)Resources.Get();

    internal static string DirectoryNotFound(string path)
        => Resources.Get().Format(path);

    internal static string WriterInReadMode => (string)Resources.Get();

    internal static string FileHandleClosed => (string)Resources.Get();

    internal static string ReadBufferNotEmpty => (string)Resources.Get();

    internal static string WriteBufferNotEmpty => (string)Resources.Get();

    internal static string StreamOverflow => (string)Resources.Get();
}