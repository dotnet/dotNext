using System;

namespace DotNext.Diagnostics
{
    /// <summary>
    /// Indicates that unreachable code is executed.
    /// </summary>
    /// <remarks>
    /// The exception indicates bug in the logic of the program.
    /// </remarks>
    public sealed class UnreachableCodeExecutionException : Exception
    {
        /// <summary>
        /// Initializes a new exception.
        /// </summary>
        public UnreachableCodeExecutionException()
            : base(ExceptionMessages.UnreachableCodeDetected)
        {
        }
    }
}
