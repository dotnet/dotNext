using System.Diagnostics.CodeAnalysis;
using System.Resources;
using Assembly = System.Reflection.Assembly;

namespace DotNext;

using static Resources.ResourceManagerExtensions;

[ExcludeFromCodeCoverage]
internal static class ExceptionMessages
{
    private static readonly ResourceManager Resources = new("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

    internal static string CollectionIsEmpty => (string)Resources.Get();

    internal static string NotInLock => (string)Resources.Get();

    internal static string TokenNotCancelable => (string)Resources.Get();

    internal static string UnsupportedLockAcquisition => (string)Resources.Get();

    internal static string EmptyValueDelegate => (string)Resources.Get();

    internal static string TerminatedExchange => (string)Resources.Get();

    internal static string EmptyWaitQueue => (string)Resources.Get();
}