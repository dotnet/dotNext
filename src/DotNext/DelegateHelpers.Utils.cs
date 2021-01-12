using System;
using System.Runtime.InteropServices;

namespace DotNext
{
    public static partial class DelegateHelpers
    {
        private interface ITargetRewriter
        {
            object? Rewrite(Delegate d);
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly struct TargetRewriter : ITargetRewriter
        {
            private readonly object target;

            internal TargetRewriter(object newTarget) => target = newTarget;

            object? ITargetRewriter.Rewrite(Delegate d) => target;
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly struct EmptyTargetRewriter : ITargetRewriter
        {
            object? ITargetRewriter.Rewrite(Delegate d) => d.Target;
        }

        private static TDelegate ChangeType<TDelegate, TRewriter>(this Delegate d, TRewriter rewriter)
            where TDelegate : Delegate
            where TRewriter : struct, ITargetRewriter
        {
            var list = d.GetInvocationList();
            if (list.LongLength == 1)
                return ReferenceEquals(list[0], d) ? d.Method.CreateDelegate<TDelegate>(rewriter.Rewrite(d)) : ChangeType<TDelegate, TRewriter>(list[0], rewriter);
            foreach (ref var sub in list.AsSpan())
                sub = sub.Method.CreateDelegate<TDelegate>(rewriter.Rewrite(sub));
            return (TDelegate)Delegate.Combine(list)!;
        }
    }
}