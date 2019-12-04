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

        internal static string OptionalNoValue => Resources.GetString("OptionalNoValue");

        internal static string InvalidUserDataSlot => Resources.GetString("InvalidUserDataSlot");

        internal static string IndexShouldBeZero => Resources.GetString("IndexShouldBeZero");

        internal static string CastNullToValueType => Resources.GetString("CastNullToValueType");

        internal static string UnsupportedLockAcquisition => Resources.GetString("UnsupportedLockAcquisition");

        internal static string InvalidMethodSignature => Resources.GetString("CannotMakeMethodPointer");

        internal static string UnsupportedMethodPointerType => Resources.GetString("UnsupportedMethodPointerType");

        internal static string UnreachableCodeDetected => Resources.GetString("UnreachableCodeDetected");

        internal static string BufferTooSmall => Resources.GetString("BufferTooSmall");
    }
}