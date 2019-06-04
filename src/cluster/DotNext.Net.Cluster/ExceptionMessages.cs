using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;

namespace DotNext
{
    [SuppressMessage("Globalization", "CA1304", Justification = "This is culture-specific resource strings")]
    [SuppressMessage("Globalization", "CA1305", Justification = "This is culture-specific resource strings")]
    internal static class ExceptionMessages
    {
        private static readonly ResourceManager Resources = new ResourceManager("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

        internal static string IsNotLeader => Resources.GetString("IsNotLeader");

        internal static string UnavailableMember => Resources.GetString("UnavailableMember");

        internal static string ReplicationRejected => Resources.GetString("ReplicationRejected");
    }
}