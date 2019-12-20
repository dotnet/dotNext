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

        internal static string AbstractDelegate => Resources.GetString("AbstractDelegate");

        internal static string MissingGetAwaiterMethod(Type t) => string.Format(Resources.GetString("MissingGetAwaiterMethod"), t.FullName);

        internal static string MissingGetResultMethod(Type t) => string.Format(Resources.GetString("MissingGetResultMethod"), t.FullName);

        internal static string InterfaceNotImplemented(Type t, Type i) => string.Format(Resources.GetString("InterfaceNotImplemented"), t.FullName, i.FullName);

        internal static string MissingMethod(string methodName, Type t) => string.Format(Resources.GetString("MissingMethod"), methodName, t.FullName);

        internal static string MissingProperty(string propertyName, Type t) => string.Format(Resources.GetString("MissingProperty"), propertyName, t.FullName);

        internal static string MissingCtor(Type t) => string.Format(Resources.GetString("MissingCtor"), t.FullName);

        internal static string EnumerablePatternExpected => Resources.GetString("EnumerablePatternExpected");

        internal static string DisposePatternExpected(Type t) => string.Format(Resources.GetString("DisposePatternExpected"), t.FullName);

        internal static string UnsupportedAsyncType => Resources.GetString("UnsupportedAsyncType");

        internal static string UnsupportedConditionalExpr => Resources.GetString("UnsupportedConditionalExpr");

        internal static string VoidLabelExpected => Resources.GetString("VoidLabelExpected");

        internal static string VoidSwitchExpected => Resources.GetString("VoidSwitchExpected");

        internal static string LeavingFinallyClause => Resources.GetString("LeavingFinallyClause");

        internal static string VoidLoopExpected => Resources.GetString("VoidLoopExpected");

        internal static string FilterHasAwait => Resources.GetString("FilterHasAwait");

        internal static string OutOfLexicalScope => Resources.GetString("OutOfLexicalScope");

        internal static string LoopNotAvailable => Resources.GetString("LoopNotAvailable");

        internal static string InvalidRethrow => Resources.GetString("InvalidRethrow");

        internal static string BoolExpressionExpected => Resources.GetString("BoolExpressionExpected");

        internal static string InvalidFragmentRendering => Resources.GetString("InvalidFragmentRendering");

        internal static string CollectionImplementationExpected => Resources.GetString("CollectionImplementationExpected");

        internal static string UnsupportedSafeNavigationType(Type type) => string.Format(Resources.GetString("UnsupportedSafeNavigationType"), type);

        internal static string TypedReferenceExpected => Resources.GetString("TypedReferenceExpected");
    }
}
