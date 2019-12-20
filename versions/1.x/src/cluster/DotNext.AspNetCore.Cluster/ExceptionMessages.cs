using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;

namespace DotNext
{
    [SuppressMessage("Globalization", "CA1304", Justification = "This is culture-specific resource strings")]
    [SuppressMessage("Globalization", "CA1305", Justification = "This is culture-specific resource strings")]
    [ExcludeFromCodeCoverage]
    internal static class ExceptionMessages
    {
        private static readonly ResourceManager Resources = new ResourceManager("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

        internal static string UnresolvedHostName(string hostName) =>
            string.Format(Resources.GetString("UnresolvedHostName"), hostName);

        internal static string MissingHeader(string headerName) =>
            string.Format(Resources.GetString("MissingHeader"), headerName);

        internal static string IncorrectResponse => Resources.GetString("IncorrectResponse");

        internal static string UnresolvedLocalMember => Resources.GetString("UnresolvedLocalMember");

        internal static string MessagingNotSupported => Resources.GetString("MessagingNotSupported");

        internal static string UnavailableMember => Resources.GetString("UnavailableMember");

        internal static string LeaderIsUnavailable => Resources.GetString("LeaderIsUnavailable");
    }
}