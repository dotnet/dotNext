using System;

namespace DotNext.Reflection
{
    internal sealed class AbstractDelegateException<D> : GenericArgumentException<D>
        where D : Delegate
    {
        internal AbstractDelegateException()
            : base(ExceptionMessages.AbstractDelegate, typeof(D).FullName)
        {
        }
    }
}
