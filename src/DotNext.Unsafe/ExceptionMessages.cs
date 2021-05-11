using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;

namespace DotNext
{
    using static Resources.ResourceManagerExtensions;

    [ExcludeFromCodeCoverage]
    internal static class ExceptionMessages
    {
        private static readonly ResourceManager Resources = new("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

        internal static string NullSource => (string)Resources.Get();

        internal static string NullDestination => (string)Resources.Get();

        internal static string WrongTargetTypeSize => (string)Resources.Get();

        internal static string NullPtr => (string)Resources.Get();

        internal static string StreamNotReadable => (string)Resources.Get();

        internal static string StreamNotWritable => (string)Resources.Get();

        internal static string SegmentVeryLarge => (string)Resources.Get();
    }
}
