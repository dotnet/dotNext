using System;
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

        internal static string NullSource => Resources.GetString("NullSource");

        internal static string NullDestination => Resources.GetString("NullDestination");

        internal static string WrongTargetTypeSize => Resources.GetString("WrongTargetTypeSize");

        internal static string NullPtr => Resources.GetString("NullPtr");

        internal static string StreamNotReadable => Resources.GetString("StreamNotReadable");

        internal static string StreamNotWritable => Resources.GetString("StreamNotWritable");

        internal static string SegmentVeryLarge => Resources.GetString("SegmentVeryLarge");
    }
}
