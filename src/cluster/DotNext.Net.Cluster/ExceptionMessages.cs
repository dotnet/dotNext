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

        internal static string NoConsensus => Resources.GetString("NoConsensus");

        internal static string CannotRemoveLocalNode => Resources.GetString("CannotRemoveLocalNode");

        internal static string ReplicationRejected => Resources.GetString("ReplicationRejected");

        internal static string AuditTrailAlreadyDefined => Resources.GetString("AuditTrailAlreadyDefined");
    }
}