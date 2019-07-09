using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext
{
    /// <summary>
    /// Represents various extensions of delegates.
    /// </summary>
    public static class DelegateHelpers
    {
        private static MethodInfo GetMethod<D>(Expression<D> expression)
            where D : Delegate
        {
            switch (expression.Body)
            {
                case MethodCallExpression expr:
                    return expr.Method;
                case MemberExpression expr when (expr.Member is PropertyInfo property):
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

        /// <summary>
        /// Returns a new delegate of different type which
        /// points to the same method as original delegate.
        /// </summary>
        /// <param name="d">Delegate to convert.</param>
        /// <typeparam name="D">A new delegate type.</typeparam>
        /// <returns>A method wrapped into new delegate type.</returns>
        /// <exception cref="ArgumentException">Cannot convert delegate type.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static D ChangeType<D>(this Delegate d) where D : Delegate => d.Method.CreateDelegate<D>(d.Target);
    }
}
