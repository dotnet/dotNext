using System.Runtime.CompilerServices;

namespace DotNext;

public static partial class DelegateHelpers
{
    private abstract class Closure
    {
        internal readonly MulticastDelegate Delegate;

        private protected Closure(MulticastDelegate action) => Delegate = action;

        internal abstract object Target { get; }
    }

    private sealed class Closure<T> : Closure
        where T : class
    {
        private Closure(T target, MulticastDelegate action)
            : base(action) => Target = target;

        internal override T Target { get; }

        private void InvokeAction() => Unsafe.As<Action<T>>(Delegate).Invoke(Target);

        internal static Action Create(Action<T> action, T arg) => new Closure<T>(arg, action).InvokeAction;

        private TResult InvokeFunc<TResult>() => Unsafe.As<Func<T, TResult>>(Delegate).Invoke(Target);

        internal static Func<TResult> Create<TResult>(Func<T, TResult> func, T arg) => new Closure<T>(arg, func).InvokeFunc<TResult>;

        private void InvokeAction<T2>(T2 arg2) => Unsafe.As<Action<T, T2>>(Delegate).Invoke(Target, arg2);

        internal static Action<T2> Create<T2>(Action<T, T2> action, T arg1) => new Closure<T>(arg1, action).InvokeAction<T2>;

        private TResult InvokeFunc<T2, TResult>(T2 arg2) => Unsafe.As<Func<T, T2, TResult>>(Delegate).Invoke(Target, arg2);

        internal static Func<T2, TResult> Create<T2, TResult>(Func<T, T2, TResult> func, T arg) => new Closure<T>(arg, func).InvokeFunc<T2, TResult>;

        private void InvokeAction<T2, T3>(T2 arg2, T3 arg3) => Unsafe.As<Action<T, T2, T3>>(Delegate).Invoke(Target, arg2, arg3);

        internal static Action<T2, T3> Create<T2, T3>(Action<T, T2, T3> action, T arg1) => new Closure<T>(arg1, action).InvokeAction<T2, T3>;

        private TResult InvokeFunc<T2, T3, TResult>(T2 arg2, T3 arg3) => Unsafe.As<Func<T, T2, T3, TResult>>(Delegate).Invoke(Target, arg2, arg3);

        internal static Func<T2, T3, TResult> Create<T2, T3, TResult>(Func<T, T2, T3, TResult> func, T arg) => new Closure<T>(arg, func).InvokeFunc<T2, T3, TResult>;

        private void InvokeAction<T2, T3, T4>(T2 arg2, T3 arg3, T4 arg4) => Unsafe.As<Action<T, T2, T3, T4>>(Delegate).Invoke(Target, arg2, arg3, arg4);

        internal static Action<T2, T3, T4> Create<T2, T3, T4>(Action<T, T2, T3, T4> action, T arg1) => new Closure<T>(arg1, action).InvokeAction<T2, T3, T4>;

        private TResult InvokeFunc<T2, T3, T4, TResult>(T2 arg2, T3 arg3, T4 arg4) => Unsafe.As<Func<T, T2, T3, T4, TResult>>(Delegate).Invoke(Target, arg2, arg3, arg4);

        internal static Func<T2, T3, T4, TResult> Create<T2, T3, T4, TResult>(Func<T, T2, T3, T4, TResult> func, T arg) => new Closure<T>(arg, func).InvokeFunc<T2, T3, T4, TResult>;

        private void InvokeAction<T2, T3, T4, T5>(T2 arg2, T3 arg3, T4 arg4, T5 arg5) => Unsafe.As<Action<T, T2, T3, T4, T5>>(Delegate).Invoke(Target, arg2, arg3, arg4, arg5);

        internal static Action<T2, T3, T4, T5> Create<T2, T3, T4, T5>(Action<T, T2, T3, T4, T5> action, T arg1) => new Closure<T>(arg1, action).InvokeAction<T2, T3, T4, T5>;

        private TResult InvokeFunc<T2, T3, T4, T5, TResult>(T2 arg2, T3 arg3, T4 arg4, T5 arg5) => Unsafe.As<Func<T, T2, T3, T4, T5, TResult>>(Delegate).Invoke(Target, arg2, arg3, arg4, arg5);

        internal static Func<T2, T3, T4, T5, TResult> Create<T2, T3, T4, T5, TResult>(Func<T, T2, T3, T4, T5, TResult> func, T arg) => new Closure<T>(arg, func).InvokeFunc<T2, T3, T4, T5, TResult>;
    }
}