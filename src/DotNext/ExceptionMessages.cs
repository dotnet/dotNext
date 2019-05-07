using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;

namespace DotNext
{
    [SuppressMessage("Globalization", "CA1304", Justification = "This is culture-specific resource strings")]
    [SuppressMessage("Globalization", "CA1305", Justification = "This is culture-specific resource strings")]
    internal static class ExceptionMessages
    {
        private static readonly ResourceManager resourceManager = new ResourceManager("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

        internal static string OptionalNoValue => resourceManager.GetString("OptionalNoValue");

        internal static string InvalidUserDataSlot => resourceManager.GetString("InvalidUserDataSlot");

        internal static string ConcreteDelegateExpected => resourceManager.GetString("ConcreteDelegateExpected");

        internal static string IndexShouldBeZero => resourceManager.GetString("IndexShouldBeZero");

        internal static string CastNullToValueType => resourceManager.GetString("CastNullToValueType");
    }
}