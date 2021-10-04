namespace DotNext.Reflection;

internal sealed class AbstractDelegateException<TDelegate> : GenericArgumentException<TDelegate>
    where TDelegate : Delegate
{
    internal AbstractDelegateException()
        : base(ExceptionMessages.AbstractDelegate, typeof(TDelegate).FullName)
    {
    }
}