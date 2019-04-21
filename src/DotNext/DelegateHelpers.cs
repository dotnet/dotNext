using System;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace DotNext
{
    /// <summary>
    /// Represents various extensions of delegates.
    /// </summary>
    public static class DelegateHelpers
    {
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

        /// <summary>
        /// Converts parameterless action into parametrized action.
        /// </summary>
        /// <param name="action">Parameterless action to convert.</param>
        /// <typeparam name="T">Type of the first parameter.</typeparam>
        /// <returns>Parametrized action.</returns>
        public static Action<T> Parametrize<T>(this Action action) => arg => action();

        public static Action<T1, T2> Parametrize<T1, T2>(this Action<T1> action) => (arg1, arg2) => action(arg1);

        public static Action<T2, T1> Permute<T1, T2>(this Action<T1, T2> action) => (arg2, arg1) => action(arg1, arg2);
    }
}
