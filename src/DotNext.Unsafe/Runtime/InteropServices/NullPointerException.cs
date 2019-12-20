using System.Runtime.Serialization;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// The exception that is thrown when there is an attempt to dereference zero pointer.
    /// </summary>
    public sealed class NullPointerException : System.NullReferenceException
    {
        private NullPointerException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        /// <summary>
        /// Initializes a new exception representing attempt to dereference zero pointer.
        /// </summary>
        /// <param name="message">The human-readable description of this message.</param>
        public NullPointerException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new exception representing attempt to dereference zero pointer.
        /// </summary>
        public NullPointerException()
            : this(ExceptionMessages.NullPtr)
        {
        }
    }
}