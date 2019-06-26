using System;

namespace DotNext.Reflection
{
    /// <summary>
    /// Root type for all exceptions related to generic constraints.
    /// </summary>
    public abstract class ConstraintViolationException : GenericArgumentException
    {
        private protected ConstraintViolationException(Type target, string message)
            : base(target, message)
        {
        }
    }
}
