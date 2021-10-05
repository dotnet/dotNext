namespace DotNext.Collections.Specialized;

internal static class InvocationListInvoker
{
    internal static void Invoke<T1, T2>(this ref InvocationList<Action<T1, T2>> list, T1 arg1, T2 arg2)
    {
        foreach (var action in list.AsSpan())
            action(arg1, arg2);
    }
}