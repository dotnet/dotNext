using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;

namespace DotNext;

using static Resources.ResourceManagerExtensions;

[ExcludeFromCodeCoverage]
internal static class ExceptionMessages
{
    private static readonly ResourceManager Resources = new("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

    internal static string MissingHeader(string headerName)
        => Resources.Get().Format(headerName);

    internal static string IncorrectResponse => (string)Resources.Get();

    internal static string InvalidRpcTimeout => (string)Resources.Get();

    internal static string UnsupportedRedirection => (string)Resources.Get();

    internal static string ReadLogEntryTwice => (string)Resources.Get();

    internal static string UnknownLocalNodeAddress => (string)Resources.Get();

    internal static string AbsoluteUriExpected(Uri uri) => Resources.Get().Format(uri);
}