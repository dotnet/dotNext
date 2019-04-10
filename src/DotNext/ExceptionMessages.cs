using System;
using System.Resources;
using System.Reflection;

namespace DotNext
{
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