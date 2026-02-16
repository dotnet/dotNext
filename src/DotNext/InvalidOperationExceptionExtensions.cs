using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext;

internal static class InvalidOperationExceptionExtensions
{
    extension(InvalidOperationException)
    {
        [DoesNotReturn]
        [StackTraceHidden]
        public static void Throw() => throw new InvalidOperationException();
    }
}