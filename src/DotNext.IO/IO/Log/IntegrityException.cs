using System;
using System.IO;

namespace DotNext.IO.Log
{
    /// <summary>
    /// Indicates that the audit trail is corrupted.
    /// </summary>
    public class IntegrityException : IOException
    {
        /// <summary>
        /// Initializes a new exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public IntegrityException(string? message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}