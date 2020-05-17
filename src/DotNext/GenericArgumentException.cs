using System;

namespace DotNext
{
    /// <summary>
    /// The exception that is thrown when one of the generic arguments
    /// provided to a type is not valid.
    /// </summary>
    public class GenericArgumentException : ArgumentException
    {
        /// <summary>
        /// Initializes a new exception.
        /// </summary>
        /// <param name="genericParam">Incorrect actual generic argument.</param>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="paramName">The name of generic parameter.</param>
        public GenericArgumentException(Type genericParam, string message, string? paramName = null)
            : base(message, string.IsNullOrEmpty(paramName) ? genericParam.FullName : paramName)
        {
            Argument = genericParam;
        }

        /// <summary>
        /// Generic argument.
        /// </summary>
        public Type Argument { get; }
    }

    /// <summary>
    /// The exception that is thrown when one of the generic arguments
    /// provided to a type is not valid.
    /// </summary>
    /// <typeparam name="T">Captured generic argument treated as invalid.</typeparam>
    public class GenericArgumentException<T> : GenericArgumentException
    {
        /// <summary>
        /// Initializes a new exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="paramName">The name of generic parameter.</param>
        public GenericArgumentException(string message, string? paramName = null)
            : base(typeof(T), message, paramName)
        {
        }
    }
}