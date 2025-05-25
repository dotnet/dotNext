using System.Runtime.InteropServices;

namespace DotNext;

public static partial class DelegateHelpers
{
    [StructLayout(LayoutKind.Auto)]
    private readonly struct TargetRewriter : ISupplier<Delegate, object?>
    {
        private readonly object target;

        internal TargetRewriter(object newTarget) => target = newTarget;

        object? ISupplier<Delegate, object?>.Invoke(Delegate d) => target;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct EmptyTargetRewriter : ISupplier<Delegate, object?>
    {
        object? ISupplier<Delegate, object?>.Invoke(Delegate d) => d.Target;
    }

    private static TDelegate ChangeType<TDelegate, TRewriter>(this Delegate d, TRewriter rewriter)
        where TDelegate : Delegate
        where TRewriter : struct, ISupplier<Delegate, object?>
    {
        var list = d.GetInvocationList();
        if (list is [var singleDelegate])
            return ReferenceEquals(singleDelegate, d)
                ? d.Method.CreateDelegate<TDelegate>(rewriter.Invoke(d))
                : ChangeType<TDelegate, TRewriter>(singleDelegate, rewriter);

        // We use untyped CreateDelegate to avoid typecast inside the loop.
        // Also, it's reasonable to reuse already allocated invocation list to store
        // newly created delegates because Delegate.Combine accepts array only
        var delegateType = typeof(TDelegate);
        foreach (ref var sub in list.AsSpan())
            sub = sub.Method.CreateDelegate(delegateType, rewriter.Invoke(sub));

        return (TDelegate)Delegate.Combine(list)!;
    }
}