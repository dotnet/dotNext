using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;

namespace DotNext
{
    using static Resources.ResourceManagerExtensions;

    [ExcludeFromCodeCoverage]
    internal static class ExceptionMessages
    {
        private static readonly ResourceManager Resources = new ResourceManager("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

        internal static string UnresolvedHostName(string hostName)
            => Resources.Get().Format(hostName);

        internal static string MissingHeader(string headerName)
            => Resources.Get().Format(headerName);

        internal static string IncorrectResponse => (string)Resources.Get();

        internal static string UnresolvedLocalMember => (string)Resources.Get();

        internal static string UnavailableMember => (string)Resources.Get();

        internal static string LeaderIsUnavailable => (string)Resources.Get();

        internal static string InvalidRpcTimeout => (string)Resources.Get();

        internal static string UnsupportedRedirection => (string)Resources.Get();
    }
}