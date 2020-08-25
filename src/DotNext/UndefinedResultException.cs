using System;
using System.Runtime.Serialization;

namespace DotNext
{
    /// <summary>
    /// Indicates that no result is provided by the operation.
    /// </summary>
    [Serializable]
    public sealed class UndefinedResultException : Exception
    {
        internal static readonly Action ThrowAction = Throw;

        private UndefinedResultException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        internal UndefinedResultException()
            : base(ExceptionMessages.UndefinedResult)
        {
        }

        private static void Throw() => throw new UndefinedResultException();
    }
}