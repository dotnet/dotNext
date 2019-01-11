using System.Runtime.Serialization;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// The exception that is thrown when there is an attempt to dereference zero pointer.
    /// </summary>
    public sealed class NullPointerException: System.NullReferenceException
    {
        private NullPointerException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public NullPointerException(string message)
            : base(message)
        {
        }

        public NullPointerException()
            : this("Zero pointer detected")
        {
        }
    }
}