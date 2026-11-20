using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace DotNext.Runtime.ExceptionServices;

/// <summary>
/// Extends <see cref="Exception"/> class.
/// </summary>
public static class ExceptionExtensions
{
    /// <summary>
    /// Throws <paramref name="source"/>
    /// </summary>
    /// <param name="source"></param>
    /// <param name="replaceStackTrace">
    /// <see langword="true"/> to replace stack trace already presented in <paramref name="source"/>
    /// with the current stack trace; otherwise, <see langword="false"/>.
    /// </param>
    /// <exception cref="Exception">The exception which is equal to <paramref name="source"/>.</exception>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void Throw(this Exception source, bool replaceStackTrace = true)
    {
        if (replaceStackTrace)
            throw source;

        ExceptionDispatchInfo.Throw(source);
    }
}