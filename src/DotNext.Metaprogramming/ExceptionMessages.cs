using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;

namespace DotNext
{
    using static Resources.ResourceManagerExtensions;

    [ExcludeFromCodeCoverage]
    internal static class ExceptionMessages
    {
        private static readonly ResourceManager Resources = new ("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

        internal static string AbstractDelegate => (string)Resources.Get();

        internal static string MissingGetAwaiterMethod(Type t) => Resources.Get().Format(t.FullName);

        internal static string MissingGetResultMethod(Type t) => Resources.Get().Format(t.FullName);

        internal static string InterfaceNotImplemented(Type t, Type i) => Resources.Get().Format(t.FullName, i.FullName);

        internal static string EnumerablePatternExpected => (string)Resources.Get();

        internal static string DisposePatternExpected(Type t) => Resources.Get().Format(t.FullName);

        internal static string UnsupportedAsyncType => (string)Resources.Get();

        internal static string UnsupportedConditionalExpr => (string)Resources.Get();

        internal static string VoidLabelExpected => (string)Resources.Get();

        internal static string VoidSwitchExpected => (string)Resources.Get();

        internal static string LeavingFinallyClause => (string)Resources.Get();

        internal static string VoidLoopExpected => (string)Resources.Get();

        internal static string FilterHasAwait => (string)Resources.Get();

        internal static string OutOfLexicalScope => (string)Resources.Get();

        internal static string LoopNotAvailable => (string)Resources.Get();

        internal static string InvalidRethrow => (string)Resources.Get();

        internal static string TypeExpected(Type type)
            => Resources.Get().Format(type.FullName);

        internal static string TypeExpected<T>()
            => TypeExpected(typeof(T));

        internal static string InvalidFragmentRendering => (string)Resources.Get();

        internal static string CollectionImplementationExpected => (string)Resources.Get();

        internal static string UnsupportedSafeNavigationType(Type type)
            => Resources.Get().Format(type.FullName);

        internal static string TypedReferenceExpected => (string)Resources.Get();

        internal static string UndeclaredVariable(string name) => Resources.Get().Format(name);

        internal static string VoidLambda => (string)Resources.Get();

        internal static string CollectionExpected(Type type) => Resources.Get().Format(type.FullName);

        internal static string AsyncEnumerableExpected => (string)Resources.Get();

        internal static string VariableNameIsNullOrEmpty => (string)Resources.Get();
    }
}
