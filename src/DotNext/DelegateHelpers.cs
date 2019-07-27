using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext
{
    using static Reflection.TypeExtensions;

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

        private static D BindUnsafe<T, D>(this Delegate del, T obj, bool devirtualize)
            where T : class
            where D : Delegate
        {
            if(obj is null)
                throw new ArgumentNullException(nameof(obj));
            if(!(del.Target is null))
                throw new InvalidOperationException();
            var method = del.Method;
            if(devirtualize)
                method = obj.GetType().Devirtualize(method) ?? method;
            return method.CreateDelegate<D>(obj); 
        }

        public static Action Bind<T>(this Action<T> action, T obj, bool devirtualize = false)
            where T : class
            => action.BindUnsafe<T, Action>(obj, devirtualize);
        
        public static Func<R> Bind<T, R>(this Func<T, R> func, T obj, bool devirtualize = false)
            where T : class
            => func.BindUnsafe<T, Func<R>>(obj, devirtualize);
        
        public static Func<T2, R> Bind<T1, T2, R>(this Func<T1, T2, R> func, T1 obj, bool devirtualize = false)
            where T1 : class
            => func.BindUnsafe<T1, Func<T2, R>>(obj, devirtualize);
        
        public static Action<T2> Bind<T1, T2>(this Action<T1, T2> action, T1 obj, bool devirtualize = false)
            where T1 : class
            => action.BindUnsafe<T1, Action<T2>>(obj, devirtualize);

        public static Func<T2, T3, R> Bind<T1, T2, T3, R>(this Func<T1, T2, T3, R> func, T1 obj, bool devirtualize = false)
            where T1 : class
            => func.BindUnsafe<T1, Func<T2, T3, R>>(obj, devirtualize);
        
        public static Action<T2, T3> Bind<T1, T2, T3>(this Action<T1, T2, T3> action, T1 obj, bool devirtualize = false)
            where T1 : class
            => action.BindUnsafe<T1, Action<T2, T3>>(obj, devirtualize);
        
        public static Func<T2, T3, T4, R> Bind<T1, T2, T3, T4, R>(this Func<T1, T2, T3, T4, R> func, T1 obj, bool devirtualize = false)
            where T1 : class
            => func.BindUnsafe<T1, Func<T2, T3, T4, R>>(obj, devirtualize);
        
        public static Action<T2, T3, T4> Bind<T1, T2, T3, T4>(this Action<T1, T2, T3, T4> action, T1 obj, bool devirtualize = false)
            where T1 : class
            => action.BindUnsafe<T1, Action<T2, T3, T4>>(obj, devirtualize);
        
        public static Func<T2, T3, T4, T5, R> Bind<T1, T2, T3, T4, T5, R>(this Func<T1, T2, T3, T4, T5, R> func, T1 obj, bool devirtualize = false)
            where T1 : class
            => func.BindUnsafe<T1, Func<T2, T3, T4, T5, R>>(obj, devirtualize);
        
        public static Action<T2, T3, T4, T5> Bind<T1, T2, T3, T4, T5>(this Action<T1, T2, T3, T4, T5> action, T1 obj, bool devirtualize = false)
            where T1 : class
            => action.BindUnsafe<T1, Action<T2, T3, T4, T5>>(obj, devirtualize);

        private static U UnsafeUnbind<U>(this Delegate del, Type targetType)
            where U : MulticastDelegate
            => ObjectExtensions.IsContravariant(del.Target, targetType) ? del.Method.CreateDelegate<U>() : throw new InvalidOperationException();

        public static Action<T> Unbind<T>(this Action action) where T : class => action.UnsafeUnbind<Action<T>>(typeof(T));
        
        public static Func<T, R> Unbind<T, R>(this Func<R> func) where T : class => func.UnsafeUnbind<Func<T, R>>(typeof(T));
        
        public static Func<G, T, R> Unbind<G, T, R>(this Func<T, R> func)
            where G : class
            => func.UnsafeUnbind<Func<G, T, R>>(typeof(G));

        public static Action<G, T> Unbind<G, T>(this Action<T> action)
            where G : class
            => action.UnsafeUnbind<Action<G, T>>(typeof(G));
        
        public static Func<G, T1, T2, R> Unbind<G, T1, T2, R>(this Func<T1, T2, R> func)
            where G : class
            => func.UnsafeUnbind<Func<G, T1, T2, R>>(typeof(G));

        public static Action<G, T1, T2> Unbind<G, T1, T2>(this Action<T1, T2> action)
            where G : class
            => action.UnsafeUnbind<Action<G, T1, T2>>(typeof(G));
        
        public static Func<G, T1, T2, T3, R> Unbind<G, T1, T2, T3, R>(this Func<T1, T2, T3, R> func)
            where G : class
            => func.UnsafeUnbind<Func<G, T1, T2, T3, R>>(typeof(G));
        
        public static Action<G, T1, T2, T3> Unbind<G, T1, T2, T3>(this Action<T1, T2, T3> action)
            where G : class
            => action.UnsafeUnbind<Action<G, T1, T2, T3>>(typeof(G));
        
        public static Func<G, T1, T2, T3, T4, R> Unbind<G, T1, T2, T3, T4, R>(this Func<T1, T2, T3, T4, R> func)
            where G : class
            => func.UnsafeUnbind<Func<G, T1, T2, T3, T4, R>>(typeof(G));
        
        public static Action<G, T1, T2, T3, T4> Unbind<G, T1, T2, T3, T4>(this Action<T1, T2, T3, T4> action)
            where G : class
            => action.UnsafeUnbind<Action<G, T1, T2, T3, T4>>(typeof(G));
        
        public static Func<G, T1, T2, T3, T4, T5, R> Unbind<G, T1, T2, T3, T4, T5, R>(this Func<T1, T2, T3, T4, T5, R> func)
            where G : class
            => func.UnsafeUnbind<Func<G, T1, T2, T3, T4, T5, R>>(typeof(G));
        
        public static Action<G, T1, T2, T3, T4, T5> Unbind<G, T1, T2, T3, T4, T5>(this Action<T1, T2, T3, T4, T5> action)
            where G : class
            => action.UnsafeUnbind<Action<G, T1, T2, T3, T4, T5>>(typeof(G));
    }
}
