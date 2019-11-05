using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Diagnostics
{
    [SuppressMessage("Design", "CA1064", Justification = "Assertion exception cannot be handled explicitly")]
    internal sealed class AssertionException : Exception
    {
        internal AssertionException(string memberName, int lineNumber, string reason)
            : base(ExceptionMessages.AssertionFailed(memberName, lineNumber, reason))
        {
            Member = memberName;
            LineNumber = lineNumber;
        }

        internal string Member { get; }

        internal int LineNumber { get; }
    }

    /// <summary>
    /// Represents various runtime assertions.
    /// </summary>
    public static class Assert
    {
        /// <summary>
        /// Creates an exception indicating that unreachable code has detected.
        /// </summary>
        /// <param name="memberName">Method or property name of the caller.</param>
        /// <param name="lineNumber">Line number in the source file at which the method is called.</param>
        /// <returns>The assertion exception.</returns>
        public static Exception Unreachable([CallerMemberName]string memberName = "", [CallerLineNumber]int lineNumber = 0) => new AssertionException(memberName, lineNumber, ExceptionMessages.UnreachableCodeDetected);

        /// <summary>
        /// Creates assertion exception.
        /// </summary>
        /// <remarks>
        /// This method is typically used to indicate that some execution path
        /// is not reachable.
        /// </remarks>
        /// <param name="reason">The message describing reason of assertion failure.</param>
        /// <param name="memberName">Method or property name of the caller.</param>
        /// <param name="lineNumber">Line number in the source file at which the method is called.</param>
        /// <returns>The exception representing that assertion has failed.</returns>
        public static Exception Failed(string reason, [CallerMemberName]string memberName = "", [CallerLineNumber]int lineNumber = 0) => new AssertionException(memberName, lineNumber, reason);

        /// <summary>
        /// Indicates that assertion has failed.
        /// </summary>
        /// <typeparam name="T">The return type to be compatible</typeparam>
        /// <param name="message">The message describing violation of assertion.</param>
        /// <param name="memberName">Method or property name of the caller.</param>
        /// <param name="lineNumber">Line number in the source file at which the method is called.</param>
        /// <returns>This method never returns the value.</returns>
        [DoesNotReturn]
        public static T Fail<T>(string message, [CallerMemberName]string memberName = "", [CallerLineNumber]int lineNumber = 0) => throw new AssertionException(memberName, lineNumber, message);

        /// <summary>
        /// Indicates that assertion has failed.
        /// </summary>
        /// <param name="message">The message describing violation of assertion.</param>
        /// <param name="memberName">Method or property name of the caller.</param>
        /// <param name="lineNumber">Line number in the source file at which the method is called.</param>
        [DoesNotReturn]
        public static void Fail(string message, [CallerMemberName]string memberName = "", [CallerLineNumber]int lineNumber = 0) => throw new AssertionException(memberName, lineNumber, message);
    }
}
