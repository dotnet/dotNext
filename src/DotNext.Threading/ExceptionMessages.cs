using System.Diagnostics.CodeAnalysis;
using System.Resources;
using Assembly = System.Reflection.Assembly;

namespace DotNext;

using static Resources.ResourceManagerExtensions;

[ExcludeFromCodeCoverage]
internal static class ExceptionMessages
{
    private static readonly ResourceManager Resources = new("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

    internal static string NotInLock => (string)Resources.Get();

    internal static string TokenNotCancelable => (string)Resources.Get();

    internal static string UnsupportedLockAcquisition => (string)Resources.Get();

    internal static string TerminatedExchange => (string)Resources.Get();

    internal static string EmptyWaitQueue => (string)Resources.Get();

    internal static string InvalidSourceState => (string)Resources.Get();

    internal static string InvalidSourceToken => (string)Resources.Get();

    internal static string AsyncTaskInterrupted => (string)Resources.Get();

    internal static string LeaseExpired => (string)Resources.Get();
    
    internal static string ConcurrencyLimitReached => (string)Resources.Get();
}