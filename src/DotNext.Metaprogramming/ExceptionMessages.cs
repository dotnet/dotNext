using System;
using System.Reflection;
using System.Resources;

namespace DotNext
{
    internal static class ExceptionMessages
    {
        private static readonly ResourceManager resourceManager = new ResourceManager("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

        internal static string AbstractDelegate => resourceManager.GetString("AbstractDelegate");

        internal static string MissingGetAwaiterMethod(Type t) => string.Format(resourceManager.GetString("MissingGetAwaiterMethod"), t.FullName);

        internal static string MissingGetResultMethod(Type t) => string.Format(resourceManager.GetString("MissingGetResultMethod"), t.FullName);

        internal static string InterfaceNotImplemented(Type t, Type i) => string.Format(resourceManager.GetString("InterfaceNotImplemented"), t.FullName, i.FullName);

        internal static string MissingMethod(string methodName, Type t) => string.Format(resourceManager.GetString("MissingMethod"), methodName, t.FullName);

        internal static string MissingProperty(string propertyName, Type t) => string.Format(resourceManager.GetString("MissingProperty"), propertyName, t.FullName);

        internal static string MissingCtor(Type t) => string.Format(resourceManager.GetString("MissingCtor"), t.FullName);

        internal static string EnumerablePatternExpected => resourceManager.GetString("EnumerablePatternExpected");

        internal static string CallFromLambdaExpected => resourceManager.GetString("CallFromLambdaExpected");

        internal static string DisposePatternExpected(Type t) => string.Format(resourceManager.GetString("DisposePatternExpected"), t.FullName);

        internal static string UnsupportedAsyncType => resourceManager.GetString("UnsupportedAsyncType");

        internal static string UnsupportedConditionalExpr => resourceManager.GetString("UnsupportedConditionalExpr");

        internal static string VoidLabelExpected => resourceManager.GetString("VoidLabelExpected");

        internal static string VoidSwitchExpected => resourceManager.GetString("VoidSwitchExpected");

        internal static string LeavingFinallyClause => resourceManager.GetString("LeavingFinallyClause");

        internal static string VoidLoopExpected => resourceManager.GetString("VoidLoopExpected");

        internal static string FilterHasAwait => resourceManager.GetString("FilterHasAwait");

        internal static string OutOfLexicalScope => resourceManager.GetString("OutOfLexicalScope");

        internal static string LoopNotAvailable => resourceManager.GetString("LoopNotAvailable");

        internal static string InvalidRethrow => resourceManager.GetString("InvalidRethrow");
    }
}
