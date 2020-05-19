using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace DotNext
{
    /// <summary>
    /// Represents various extensions of delegates.
    /// </summary>
    public static class DelegateHelpers
    {
        private interface ITargetRewriter
        {
            object? Rewrite(Delegate d);
        }

        private abstract class Closure
        {
            internal readonly MulticastDelegate Delegate;

            private protected Closure(MulticastDelegate action) => Delegate = action;

            internal abstract object Target { get; }
        }

        private sealed class Closure<T> : Closure
            where T : class
        {
            private readonly T target;

            private Closure(T target, MulticastDelegate action)
                : base(action) => this.target = target;

            internal override object Target => target;

            private void InvokeAction() => Unsafe.As<Action<T>>(Delegate).Invoke(target);

            internal static Action Create(Action<T> action, T arg) => new Closure<T>(arg, action).InvokeAction;

            private TResult InvokeFunc<TResult>() => Unsafe.As<Func<T, TResult>>(Delegate).Invoke(target);

            internal static Func<TResult> Create<TResult>(Func<T, TResult> func, T arg) => new Closure<T>(arg, func).InvokeFunc<TResult>;

            private void InvokeAction<T2>(T2 arg2) => Unsafe.As<Action<T, T2>>(Delegate).Invoke(target, arg2);

            internal static Action<T2> Create<T2>(Action<T, T2> action, T arg1) => new Closure<T>(arg1, action).InvokeAction<T2>;

            private TResult InvokeFunc<T2, TResult>(T2 arg2) => Unsafe.As<Func<T, T2, TResult>>(Delegate).Invoke(target, arg2);

            internal static Func<T2, TResult> Create<T2, TResult>(Func<T, T2, TResult> func, T arg) => new Closure<T>(arg, func).InvokeFunc<T2, TResult>;

            private void InvokeAction<T2, T3>(T2 arg2, T3 arg3) => Unsafe.As<Action<T, T2, T3>>(Delegate).Invoke(target, arg2, arg3);

            internal static Action<T2, T3> Create<T2, T3>(Action<T, T2, T3> action, T arg1) => new Closure<T>(arg1, action).InvokeAction<T2, T3>;

            private TResult InvokeFunc<T2, T3, TResult>(T2 arg2, T3 arg3) => Unsafe.As<Func<T, T2, T3, TResult>>(Delegate).Invoke(target, arg2, arg3);

            internal static Func<T2, T3, TResult> Create<T2, T3, TResult>(Func<T, T2, T3, TResult> func, T arg) => new Closure<T>(arg, func).InvokeFunc<T2, T3, TResult>;

            private void InvokeAction<T2, T3, T4>(T2 arg2, T3 arg3, T4 arg4) => Unsafe.As<Action<T, T2, T3, T4>>(Delegate).Invoke(target, arg2, arg3, arg4);

            internal static Action<T2, T3, T4> Create<T2, T3, T4>(Action<T, T2, T3, T4> action, T arg1) => new Closure<T>(arg1, action).InvokeAction<T2, T3, T4>;

            private TResult InvokeFunc<T2, T3, T4, TResult>(T2 arg2, T3 arg3, T4 arg4) => Unsafe.As<Func<T, T2, T3, T4, TResult>>(Delegate).Invoke(target, arg2, arg3, arg4);

            internal static Func<T2, T3, T4, TResult> Create<T2, T3, T4, TResult>(Func<T, T2, T3, T4, TResult> func, T arg) => new Closure<T>(arg, func).InvokeFunc<T2, T3, T4, TResult>;

            private void InvokeAction<T2, T3, T4, T5>(T2 arg2, T3 arg3, T4 arg4, T5 arg5) => Unsafe.As<Action<T, T2, T3, T4, T5>>(Delegate).Invoke(target, arg2, arg3, arg4, arg5);

            internal static Action<T2, T3, T4, T5> Create<T2, T3, T4, T5>(Action<T, T2, T3, T4, T5> action, T arg1) => new Closure<T>(arg1, action).InvokeAction<T2, T3, T4, T5>;

            private TResult InvokeFunc<T2, T3, T4, T5, TResult>(T2 arg2, T3 arg3, T4 arg4, T5 arg5) => Unsafe.As<Func<T, T2, T3, T4, T5, TResult>>(Delegate).Invoke(target, arg2, arg3, arg4, arg5);

            internal static Func<T2, T3, T4, T5, TResult> Create<T2, T3, T4, T5, TResult>(Func<T, T2, T3, T4, T5, TResult> func, T arg) => new Closure<T>(arg, func).InvokeFunc<T2, T3, T4, T5, TResult>;
        }

        private static readonly Predicate<Assembly>? IsCollectible;
        private static readonly SendOrPostCallback SendOrPostInvoker;
        private static readonly Action<Action> ActionInvoker;

        static DelegateHelpers()
        {
            var isCollectibleGetter = typeof(Assembly).GetProperty("IsCollectible", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)?.GetMethod;
            IsCollectible = isCollectibleGetter?.CreateDelegate<Predicate<Assembly>>();
            SendOrPostInvoker = Runtime.Intrinsics.UnsafeInvoke;
            ActionInvoker = CreateDelegate<Action<Action>>((MethodInfo)MethodBase.GetMethodFromHandle(Runtime.Intrinsics.ActionInvokeMethod));
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

        private static MethodInfo GetMethod<TDelegate>(Expression<TDelegate> expression)
            where TDelegate : Delegate
            => expression.Body switch
            {
                MethodCallExpression expr => expr.Method,
                MemberExpression expr when expr.Member is PropertyInfo property => property.GetMethod,
                BinaryExpression expr => expr.Method,
                IndexExpression expr => expr.Indexer.GetMethod,
                UnaryExpression expr => expr.Method,
                _ => throw new ArgumentException(ExceptionMessages.InvalidExpressionTree, nameof(expression))
            };

        /// <summary>
        /// Creates open delegate for the instance method, property, operator referenced
        /// in expression tree.
        /// </summary>
        /// <typeparam name="TDelegate">The type of the delegate describing expression tree.</typeparam>
        /// <param name="expression">The expression tree containing instance method call.</param>
        /// <returns>The open delegate.</returns>
        /// <exception cref="ArgumentException"><paramref name="expression"/> is not valid expression tree.</exception>
        public static TDelegate CreateOpenDelegate<TDelegate>(Expression<TDelegate> expression)
            where TDelegate : Delegate
            => GetMethod(expression).CreateDelegate<TDelegate>();

        /// <summary>
        /// Creates a factory for closed delegates.
        /// </summary>
        /// <param name="expression">The expression tree containing instance method, property, operator call.</param>
        /// <typeparam name="TDelegate">The type of the delegate describing expression tree.</typeparam>
        /// <returns>The factory of closed delegate.</returns>
        public static Func<object, TDelegate>? CreateClosedDelegateFactory<TDelegate>(Expression<TDelegate> expression)
            where TDelegate : Delegate
        {
            var method = GetMethod(expression);
            return method is null ? null : new Func<object, TDelegate>(method.CreateDelegate<TDelegate>);
        }

        /// <summary>
        /// Performs contravariant conversion
        /// of actual generic argument specified
        /// for <see cref="EventHandler{TEventArgs}"/> type.
        /// </summary>
        /// <typeparam name="TBase">Input type of the delegate.</typeparam>
        /// <typeparam name="T">A subtype of <typeparamref name="TBase"/>.</typeparam>
        /// <param name="handler">The handler to convert.</param>
        /// <returns>The delegate referencing the same method as original delegate.</returns>
        /// <remarks>
        /// Generic parameter of delegate <see cref="EventHandler{TEventArgs}"/>
        /// is not marked as <see langword="in"/> so compiler doesn't
        /// support contravariant conversion for it. This method
        /// provides contravariant conversion for this delegate type.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EventHandler<T> Contravariant<TBase, T>(this EventHandler<TBase> handler)
            where TBase : class
            where T : class, TBase
            => handler.ChangeType<EventHandler<T>>();

        /// <summary>
        /// Creates a delegate of the specified type with the specified target from this method.
        /// </summary>
        /// <typeparam name="TDelegate">The type of the delegate to create.</typeparam>
        /// <param name="method">The method to be wrapped into delegate.</param>
        /// <param name="target">The object targeted by the delegate.</param>
        /// <returns>The delegate for the specified method.</returns>
        /// <seealso cref="MethodInfo.CreateDelegate(Type, object)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TDelegate CreateDelegate<TDelegate>(this MethodInfo method, object? target = null)
            where TDelegate : Delegate
            => (TDelegate)method.CreateDelegate(typeof(TDelegate), target);

        private static TDelegate ChangeType<TDelegate, TRewriter>(this Delegate d, TRewriter rewriter)
            where TDelegate : Delegate
            where TRewriter : struct, ITargetRewriter
        {
            var list = d.GetInvocationList();
            if (list.LongLength == 1)
                return ReferenceEquals(list[0], d) ? d.Method.CreateDelegate<TDelegate>(rewriter.Rewrite(d)) : ChangeType<TDelegate, TRewriter>(list[0], rewriter);
            foreach (ref var sub in list.AsSpan())
                sub = sub.Method.CreateDelegate<TDelegate>(rewriter.Rewrite(sub));
            return (TDelegate)Delegate.Combine(list);
        }

        /// <summary>
        /// Returns a new delegate of different type which
        /// points to the same method as original delegate.
        /// </summary>
        /// <param name="d">Delegate to convert.</param>
        /// <typeparam name="TDelegate">A new delegate type.</typeparam>
        /// <returns>A method wrapped into new delegate type.</returns>
        /// <exception cref="ArgumentException">Cannot convert delegate type.</exception>
        public static TDelegate ChangeType<TDelegate>(this Delegate d)
            where TDelegate : Delegate
            => d is TDelegate ? Unsafe.As<TDelegate>(d) : ChangeType<TDelegate, EmptyTargetRewriter>(d, new EmptyTargetRewriter());

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T">The type of the first parameter to bind.</typeparam>
        /// <param name="action">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Action Bind<T>(this Action<T> action, T obj)
            where T : class
        {
            // TODO: Should be generalized using function pointer in C# 9
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (action.Target is null)
                return ChangeType<Action, TargetRewriter>(action, new TargetRewriter(obj));
            return Closure<T>.Create(action, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Func<TResult> Bind<T, TResult>(this Func<T, TResult> func, T obj)
            where T : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (func.Target is null)
                return ChangeType<Func<TResult>, TargetRewriter>(func, new TargetRewriter(obj));
            return Closure<T>.Create(func, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Func<T2, TResult> Bind<T1, T2, TResult>(this Func<T1, T2, TResult> func, T1 obj)
            where T1 : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (func.Target is null)
                return ChangeType<Func<T2, TResult>, TargetRewriter>(func, new TargetRewriter(obj));
            return Closure<T1>.Create(func, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <param name="action">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Action<T2> Bind<T1, T2>(this Action<T1, T2> action, T1 obj)
            where T1 : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (action.Target is null)
                return ChangeType<Action<T2>, TargetRewriter>(action, new TargetRewriter(obj));
            return Closure<T1>.Create(action, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Func<T2, T3, TResult> Bind<T1, T2, T3, TResult>(this Func<T1, T2, T3, TResult> func, T1 obj)
            where T1 : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (func.Target is null)
                return ChangeType<Func<T2, T3, TResult>, TargetRewriter>(func, new TargetRewriter(obj));
            return Closure<T1>.Create(func, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <param name="action">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Action<T2, T3> Bind<T1, T2, T3>(this Action<T1, T2, T3> action, T1 obj)
            where T1 : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (action.Target is null)
                return ChangeType<Action<T2, T3>, TargetRewriter>(action, new TargetRewriter(obj));
            return Closure<T1>.Create(action, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Func<T2, T3, T4, TResult> Bind<T1, T2, T3, T4, TResult>(this Func<T1, T2, T3, T4, TResult> func, T1 obj)
            where T1 : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (func.Target is null)
                return ChangeType<Func<T2, T3, T4, TResult>, TargetRewriter>(func, new TargetRewriter(obj));
            return Closure<T1>.Create(func, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
        /// <param name="action">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Action<T2, T3, T4> Bind<T1, T2, T3, T4>(this Action<T1, T2, T3, T4> action, T1 obj)
            where T1 : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (action.Target is null)
                return ChangeType<Action<T2, T3, T4>, TargetRewriter>(action, new TargetRewriter(obj));
            return Closure<T1>.Create(action, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
        /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Func<T2, T3, T4, T5, TResult> Bind<T1, T2, T3, T4, T5, TResult>(this Func<T1, T2, T3, T4, T5, TResult> func, T1 obj)
            where T1 : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (func.Target is null)
                return ChangeType<Func<T2, T3, T4, T5, TResult>, TargetRewriter>(func, new TargetRewriter(obj));
            return Closure<T1>.Create(func, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
        /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
        /// <param name="action">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Action<T2, T3, T4, T5> Bind<T1, T2, T3, T4, T5>(this Action<T1, T2, T3, T4, T5> action, T1 obj)
            where T1 : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (action.Target is null)
                return ChangeType<Action<T2, T3, T4, T5>, TargetRewriter>(action, new TargetRewriter(obj));
            return Closure<T1>.Create(action, obj);
        }

        private static T Unbind<T>(this Delegate del, Type targetType)
            where T : MulticastDelegate
        {
            var target = del.Target;
            if (target is Closure closure)
            {
                if (ObjectExtensions.IsContravariant(closure.Target, targetType))
                    return ChangeType<T, EmptyTargetRewriter>(closure.Delegate, default);
                else
                    goto invalid_op;
            }

            if (ObjectExtensions.IsContravariant(target, targetType))
                return ChangeType<T, TargetRewriter>(del, default);
            invalid_op:
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <param name="action">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravarient to type <typeparamref name="T"/>.</exception>
        public static Action<T> Unbind<T>(this Action action)
            where T : class
            => action.Unbind<Action<T>>(typeof(T));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravarient to type <typeparamref name="T"/>.</exception>
        public static Func<T, TResult> Unbind<T, TResult>(this Func<TResult> func)
            where T : class
            => func.Unbind<Func<T, TResult>>(typeof(T));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="TArg">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravarient to type <typeparamref name="T"/>.</exception>
        public static Func<T, TArg, TResult> Unbind<T, TArg, TResult>(this Func<TArg, TResult> func)
            where T : class
            => func.Unbind<Func<T, TArg, TResult>>(typeof(T));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="TArg">The type of the first explicit parameter.</typeparam>
        /// <param name="action">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravarient to type <typeparamref name="T"/>.</exception>
        public static Action<T, TArg> Unbind<T, TArg>(this Action<TArg> action)
            where T : class
            => action.Unbind<Action<T, TArg>>(typeof(T));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravarient to type <typeparamref name="T"/>.</exception>
        public static Func<T, T1, T2, TResult> Unbind<T, T1, T2, TResult>(this Func<T1, T2, TResult> func)
            where T : class
            => func.Unbind<Func<T, T1, T2, TResult>>(typeof(T));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
        /// <param name="action">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravarient to type <typeparamref name="T"/>.</exception>
        public static Action<T, T1, T2> Unbind<T, T1, T2>(this Action<T1, T2> action)
            where T : class
            => action.Unbind<Action<T, T1, T2>>(typeof(T));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
        /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravarient to type <typeparamref name="T"/>.</exception>
        public static Func<T, T1, T2, T3, TResult> Unbind<T, T1, T2, T3, TResult>(this Func<T1, T2, T3, TResult> func)
            where T : class
            => func.Unbind<Func<T, T1, T2, T3, TResult>>(typeof(T));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
        /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
        /// <param name="action">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravarient to type <typeparamref name="T"/>.</exception>
        public static Action<T, T1, T2, T3> Unbind<T, T1, T2, T3>(this Action<T1, T2, T3> action)
            where T : class
            => action.Unbind<Action<T, T1, T2, T3>>(typeof(T));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
        /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth explicit parameter.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravarient to type <typeparamref name="T"/>.</exception>
        public static Func<T, T1, T2, T3, T4, TResult> Unbind<T, T1, T2, T3, T4, TResult>(this Func<T1, T2, T3, T4, TResult> func)
            where T : class
            => func.Unbind<Func<T, T1, T2, T3, T4, TResult>>(typeof(T));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
        /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth explicit parameter.</typeparam>
        /// <param name="action">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravarient to type <typeparamref name="T"/>.</exception>
        public static Action<T, T1, T2, T3, T4> Unbind<T, T1, T2, T3, T4>(this Action<T1, T2, T3, T4> action)
            where T : class
            => action.Unbind<Action<T, T1, T2, T3, T4>>(typeof(T));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
        /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth explicit parameter.</typeparam>
        /// <typeparam name="T5">The type of the fifth explicit parameter.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravarient to type <typeparamref name="T"/>.</exception>
        public static Func<T, T1, T2, T3, T4, T5, TResult> Unbind<T, T1, T2, T3, T4, T5, TResult>(this Func<T1, T2, T3, T4, T5, TResult> func)
            where T : class
            => func.Unbind<Func<T, T1, T2, T3, T4, T5, TResult>>(typeof(T));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
        /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth explicit parameter.</typeparam>
        /// <typeparam name="T5">The type of the fifth explicit parameter.</typeparam>
        /// <param name="action">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravarient to type <typeparamref name="T"/>.</exception>
        public static Action<T, T1, T2, T3, T4, T5> Unbind<T, T1, T2, T3, T4, T5>(this Action<T1, T2, T3, T4, T5> action)
            where T : class
            => action.Unbind<Action<T, T1, T2, T3, T4, T5>>(typeof(T));

        internal static void InvokeInContext(this Action action, SynchronizationContext context)
            => context.Post(SendOrPostInvoker, action);

        internal static void InvokeInThreadPool(this Action action)
            => ThreadPool.QueueUserWorkItem(ActionInvoker, action, false);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CanBeUnloaded(Assembly assembly)
            => assembly.IsDynamic || assembly.ReflectionOnly || (IsCollectible?.Invoke(assembly) ?? false);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsRegularDelegate(Delegate d)
            => (d.Method.Attributes & MethodAttributes.Static) == 0 || d.Target != null || CanBeUnloaded(d.Method.Module.Assembly);
    }
}
