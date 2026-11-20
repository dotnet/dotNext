using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext;

/// <summary>
/// Extends <see cref="ArgumentNullException"/> class.
/// </summary>
public static class ArgumentNullExceptionExtensions
{
    /// <summary>
    /// Extends <see cref="ArgumentNullException"/> class.
    /// </summary>
    extension(ArgumentNullException)
    {
        /// <summary>
        /// Throws <see cref="ArgumentNullException"/> if <paramref name="argument"/>
        /// is <see langword="null"/>.
        /// </summary>
        /// <param name="argument">The value to check.</param>
        /// <param name="paramName">The string which represents <paramref name="argument"/> expression.</param>
        /// <typeparam name="T">The type of the value.</typeparam>
        public static void ThrowIfNull<T>([NotNull] T? argument, [CallerArgumentExpression(nameof(argument))] string paramName = "")
            where T : allows ref struct
        {
            if (argument is null)
                Throw(paramName);
        }

        [DoesNotReturn]
        [StackTraceHidden]
        internal static void Throw(string paramName) => throw new ArgumentNullException(paramName);
    }
}