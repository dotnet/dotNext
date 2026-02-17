using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext;

internal static class InvalidCastExceptionExtensions
{
    extension(InvalidCastException)
    {
        [DoesNotReturn]
        [StackTraceHidden]
        public static void Throw() => throw new InvalidCastException();
    }
}