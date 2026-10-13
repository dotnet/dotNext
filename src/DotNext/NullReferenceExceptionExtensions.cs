using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext;

internal static class NullReferenceExceptionExtensions
{
    extension(NullReferenceException)
    {
        [DoesNotReturn]
        public static void Throw<T>(T _) where T : allows ref struct => throw new NullReferenceException();

        [DoesNotReturn]
        public static T Throw<T>() where T : allows ref struct => throw new NullReferenceException();
    }
}