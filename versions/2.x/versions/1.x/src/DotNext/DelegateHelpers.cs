using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
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
            object Rewrite(Delegate d);
        }

        private static readonly Predicate<Assembly> IsCollectible;
        private static readonly WaitCallback ActionInvoker;

        static DelegateHelpers()
        {
            var isCollectibleGetter = typeof(Assembly).GetProperty("IsCollectible", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)?.GetMethod;
            IsCollectible = isCollectibleGetter?.CreateDelegate<Predicate<Assembly>>();
            ActionInvoker = Runtime.Intrinsics.UnsafeInvoke;
        }

        private readonly struct TargetRewriter : ITargetRewriter
        {
            private readonly object target;

            internal TargetRewriter(object newTarget) => this.target = newTarget;

            object ITargetRewriter.Rewrite(Delegate d) => this.target;
        }

        private readonly struct EmptyTargetRewriter : ITargetRewriter
        {
            object ITargetRewriter.Rewrite(Delegate d) => d.Target;
        }

        private static MethodInfo GetMethod<D>(Expression<D> expression)
            where D : Delegate
        {
            switch (expression.Body)
            {
                case MethodCallExpression expr:
                    return expr.Method;
                case MemberExpression expr when expr.Member is PropertyInfo property:
                    return property.GetMethod;
                case BinaryExpression expr:
                    return expr.Method;
                case IndexExpression expr:
                    return expr.Indexer.GetMethod;
                case UnaryExpression expr:
                    return expr.Method;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Creates open delegate for the instance method, property, operator referenced
        /// in expression tree.
        /// </summary>
        /// <typeparam name="D">The type of the delegate describing expression tree.</typeparam>
        /// <param name="expression">The expression tree containing instance method call.</param>
        /// <returns>The open delegate.</returns>
        public static D CreateOpenDelegate<D>(Expression<D> expression)
            where D : Delegate
            => GetMethod(expression)?.CreateDelegate<D>();

        /// <summary>
        /// Creates a factory for closed delegates.
        /// </summary>
        /// <param name="expression">The expression tree containing instance method, property, operator call.</param>
        /// <typeparam name="D">The type of the delegate describing expression tree.</typeparam>
        /// <returns>The factory of closed delegate.</returns>
        public static Func<object, D> CreateClosedDelegateFactory<D>(Expression<D> expression)
            where D : Delegate
        {
            var method = GetMethod(expression);
            return method is null ? null : new Func<object, D>(method.CreateDelegate<D>);
        }

        /// <summary>
        /// Performs contravariant conversion
        /// of actual generic argument specified
        /// for <see cref="EventHandler{TEventArgs}"/> type.
        /// </summary>
        /// <typeparam name="I">Input type of the delegate.</typeparam>
        /// <typeparam name="O">A subtype of <typeparamref name="I"/>.</typeparam>
        /// <param name="handler">The handler to convert.</param>
        /// <returns>The delegate referencing the same method as original delegate.</returns>
        /// <remarks>
        /// Generic parameter of delegate <see cref="EventHandler{TEventArgs}"/>
        /// is not marked as <see langword="in"/> so compiler doesn't
        /// support contravariant conversion for it. This method
        /// provides contravariant conversion for this delegate type.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EventHandler<O> Contravariant<I, O>(this EventHandler<I> handler)
            where I : class
            where O : class, I
            => handler.ChangeType<EventHandler<O>>();

        /// <summary>
        /// Creates a delegate of the specified type with the specified target from this method.
        /// </summary>
        /// <typeparam name="D">The type of the delegate to create.</typeparam>
        /// <param name="method">The method to be wrapped into delegate.</param>
        /// <param name="target">The object targeted by the delegate.</param>
        /// <returns>The delegate for the specified method.</returns>
        /// <seealso cref="MethodInfo.CreateDelegate(Type, object)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static D CreateDelegate<D>(this MethodInfo method, object target = null)
            where D : Delegate
            => (D)method.CreateDelegate(typeof(D), target);


        private static D ChangeType<D, TRewriter>(this Delegate d, TRewriter rewriter)
            where D : Delegate
            where TRewriter : struct, ITargetRewriter
        {
            var list = d.GetInvocationList();
            if (list.LongLength == 1)
                return ReferenceEquals(list[0], d) ? d.Method.CreateDelegate<D>(rewriter.Rewrite(d)) : ChangeType<D, TRewriter>(list[0], rewriter);
            foreach (ref var sub in list.AsSpan())
                sub = sub.Method.CreateDelegate<D>(rewriter.Rewrite(sub));
            return (D)Delegate.Combine(list);
        }

        /// <summary>
        /// Returns a new delegate of different type which
        /// points to the same method as original delegate.
        /// </summary>
        /// <param name="d">Delegate to convert.</param>
        /// <typeparam name="D">A new delegate type.</typeparam>
        /// <returns>A method wrapped into new delegate type.</returns>
        /// <exception cref="ArgumentException">Cannot convert delegate type.</exception>
        public static D ChangeType<D>(this Delegate d)
            where D : Delegate
            => d is D ? Unsafe.As<D>(d) : ChangeType<D, EmptyTargetRewriter>(d, new EmptyTargetRewriter());

        private static D UnsafeBind<T, D>(this Delegate del, T obj)
            where T : class
            where D : Delegate
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            return del.Target is null ? ChangeType<D, TargetRewriter>(del, new TargetRewriter(obj)) : throw new InvalidOperationException();
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T">The type of the first parameter to bind.</typeparam>
        /// <param name="action">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not <see langword="null"/>.</exception>
        public static Action Bind<T>(this Action<T> action, T obj)
            where T : class
            => action.UnsafeBind<T, Action>(obj);

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="R">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not <see langword="null"/>.</exception>
        public static Func<R> Bind<T, R>(this Func<T, R> func, T obj)
            where T : class
            => func.UnsafeBind<T, Func<R>>(obj);

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="R">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not <see langword="null"/>.</exception>
        public static Func<T2, R> Bind<T1, T2, R>(this Func<T1, T2, R> func, T1 obj)
            where T1 : class
            => func.UnsafeBind<T1, Func<T2, R>>(obj);

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <param name="action">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not <see langword="null"/>.</exception>
        public static Action<T2> Bind<T1, T2>(this Action<T1, T2> action, T1 obj)
            where T1 : class
            => action.UnsafeBind<T1, Action<T2>>(obj);

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <typeparam name="R">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not <see langword="null"/>.</exception>
        public static Func<T2, T3, R> Bind<T1, T2, T3, R>(this Func<T1, T2, T3, R> func, T1 obj)
            where T1 : class
            => func.UnsafeBind<T1, Func<T2, T3, R>>(obj);

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
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not <see langword="null"/>.</exception>
        public static Action<T2, T3> Bind<T1, T2, T3>(this Action<T1, T2, T3> action, T1 obj)
            where T1 : class
            => action.UnsafeBind<T1, Action<T2, T3>>(obj);

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
        /// <typeparam name="R">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not <see langword="null"/>.</exception>
        public static Func<T2, T3, T4, R> Bind<T1, T2, T3, T4, R>(this Func<T1, T2, T3, T4, R> func, T1 obj)
            where T1 : class
            => func.UnsafeBind<T1, Func<T2, T3, T4, R>>(obj);

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
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not <see langword="null"/>.</exception>
        public static Action<T2, T3, T4> Bind<T1, T2, T3, T4>(this Action<T1, T2, T3, T4> action, T1 obj)
            where T1 : class
            => action.UnsafeBind<T1, Action<T2, T3, T4>>(obj);

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
        /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
        /// <typeparam name="R">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not <see langword="null"/>.</exception>
        public static Func<T2, T3, T4, T5, R> Bind<T1, T2, T3, T4, T5, R>(this Func<T1, T2, T3, T4, T5, R> func, T1 obj)
            where T1 : class
            => func.UnsafeBind<T1, Func<T2, T3, T4, T5, R>>(obj);

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
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not <see langword="null"/>.</exception>
        public static Action<T2, T3, T4, T5> Bind<T1, T2, T3, T4, T5>(this Action<T1, T2, T3, T4, T5> action, T1 obj)
            where T1 : class
            => action.UnsafeBind<T1, Action<T2, T3, T4, T5>>(obj);

        private static U UnsafeUnbind<U>(this Delegate del, Type targetType)
            where U : MulticastDelegate
            => ObjectExtensions.IsContravariant(del.Target, targetType) ? ChangeType<U, TargetRewriter>(del, default) : throw new InvalidOperationException();

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <param name="action">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravarient to type <typeparamref name="T"/>.</exception>
        public static Action<T> Unbind<T>(this Action action) where T : class => action.UnsafeUnbind<Action<T>>(typeof(T));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="R">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravarient to type <typeparamref name="T"/>.</exception>
        public static Func<T, R> Unbind<T, R>(this Func<R> func) where T : class => func.UnsafeUnbind<Func<T, R>>(typeof(T));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="R">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravarient to type <typeparamref name="G"/>.</exception>
        public static Func<G, T, R> Unbind<G, T, R>(this Func<T, R> func)
            where G : class
            => func.UnsafeUnbind<Func<G, T, R>>(typeof(G));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T">The type of the first explicit parameter.</typeparam>
        /// <param name="action">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravarient to type <typeparamref name="G"/>.</exception>
        public static Action<G, T> Unbind<G, T>(this Action<T> action)
            where G : class
            => action.UnsafeUnbind<Action<G, T>>(typeof(G));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
        /// <typeparam name="R">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravarient to type <typeparamref name="G"/>.</exception>
        public static Func<G, T1, T2, R> Unbind<G, T1, T2, R>(this Func<T1, T2, R> func)
            where G : class
            => func.UnsafeUnbind<Func<G, T1, T2, R>>(typeof(G));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
        /// <param name="action">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravarient to type <typeparamref name="G"/>.</exception>
        public static Action<G, T1, T2> Unbind<G, T1, T2>(this Action<T1, T2> action)
            where G : class
            => action.UnsafeUnbind<Action<G, T1, T2>>(typeof(G));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
        /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
        /// <typeparam name="R">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravarient to type <typeparamref name="G"/>.</exception>
        public static Func<G, T1, T2, T3, R> Unbind<G, T1, T2, T3, R>(this Func<T1, T2, T3, R> func)
            where G : class
            => func.UnsafeUnbind<Func<G, T1, T2, T3, R>>(typeof(G));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
        /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
        /// <param name="action">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravarient to type <typeparamref name="G"/>.</exception>
        public static Action<G, T1, T2, T3> Unbind<G, T1, T2, T3>(this Action<T1, T2, T3> action)
            where G : class
            => action.UnsafeUnbind<Action<G, T1, T2, T3>>(typeof(G));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
        /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth explicit parameter.</typeparam>
        /// <typeparam name="R">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravarient to type <typeparamref name="G"/>.</exception>
        public static Func<G, T1, T2, T3, T4, R> Unbind<G, T1, T2, T3, T4, R>(this Func<T1, T2, T3, T4, R> func)
            where G : class
            => func.UnsafeUnbind<Func<G, T1, T2, T3, T4, R>>(typeof(G));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
        /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth explicit parameter.</typeparam>
        /// <param name="action">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravarient to type <typeparamref name="G"/>.</exception>
        public static Action<G, T1, T2, T3, T4> Unbind<G, T1, T2, T3, T4>(this Action<T1, T2, T3, T4> action)
            where G : class
            => action.UnsafeUnbind<Action<G, T1, T2, T3, T4>>(typeof(G));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
        /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth explicit parameter.</typeparam>
        /// <typeparam name="T5">The type of the fifth explicit parameter.</typeparam>
        /// <typeparam name="R">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravarient to type <typeparamref name="G"/>.</exception>
        public static Func<G, T1, T2, T3, T4, T5, R> Unbind<G, T1, T2, T3, T4, T5, R>(this Func<T1, T2, T3, T4, T5, R> func)
            where G : class
            => func.UnsafeUnbind<Func<G, T1, T2, T3, T4, T5, R>>(typeof(G));

        /// <summary>
        /// Converts implicitly bound delegate into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Delegate.Target"/>.</typeparam>
        /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
        /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
        /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth explicit parameter.</typeparam>
        /// <typeparam name="T5">The type of the fifth explicit parameter.</typeparam>
        /// <param name="action">The delegate to unbind.</param>
        /// <returns>Unbound version of the delegate.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravarient to type <typeparamref name="G"/>.</exception>
        public static Action<G, T1, T2, T3, T4, T5> Unbind<G, T1, T2, T3, T4, T5>(this Action<T1, T2, T3, T4, T5> action)
            where G : class
            => action.UnsafeUnbind<Action<G, T1, T2, T3, T4, T5>>(typeof(G));

        internal static void InvokeInContext(this Action action, SynchronizationContext context) => context.Post(Unsafe.As<SendOrPostCallback>(ActionInvoker), action);

        internal static void InvokeInThreadPool(this Action action) => ThreadPool.QueueUserWorkItem(ActionInvoker, action);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CanBeUnloaded(Assembly assembly)
            => assembly.IsDynamic || assembly.ReflectionOnly || (IsCollectible?.Invoke(assembly) ?? false);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsRegularDelegate(Delegate d)
            => (d.Method.Attributes & MethodAttributes.Static) == 0 || d.Target != null || CanBeUnloaded(d.Method.Module.Assembly);
    }
}
