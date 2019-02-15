using System;
using System.Reflection;

namespace DotNext
{
    /// <summary>
    /// Represents various extensions of delegates.
    /// </summary>
    public static class DelegateHelpers
    {
        public static D[] GetInvocationList<D>(D @delegate)
            where D: MulticastDelegate
            => @delegate?.GetInvocationList() as D[] ?? Array.Empty<D>();

        public static EventHandler<O> Contravariant<I, O>(this EventHandler<I> handler)
            where I : class
            where O : class, I
            => handler.ChangeType<EventHandler<O>>();

        public static D CreateDelegate<D>(this MethodInfo method, object target = null)
            where D : Delegate
            => (D)method.CreateDelegate(typeof(D), target);

        /// <summary>
        /// Returns special Invoke method generate for each delegate type.
        /// </summary>
        /// <typeparam name="D">Type of delegate.</typeparam>
        /// <returns>An object representing reflected method Invoke.</returns>
        public static MethodInfo GetInvokeMethod<D>()
            where D : Delegate
            => Reflection.TypeExtensions.GetInvokeMethod(typeof(D));

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
            => d.Method.CreateDelegate<D>(d.Target);
    }
}
