using System;

namespace DotNext
{
    /// <summary>
    /// Provides extension methods for delegate <see cref="Func{TResult}"/> and
    /// predefined functions.
    /// </summary>
    public static class Func
    {
        private static class Id<I, O>
            where I: O
        {
            internal static readonly Func<I, O> Value = Converter.Identity<I, O>;
        }

        /// <summary>
        /// The function which returns input argument
        /// without any modifications.
        /// </summary>
        /// <typeparam name="I">Type of input.</typeparam>
        /// <typeparam name="O">Type of output.</typeparam>
        /// <returns>The identity function.</returns>
        /// <remarks>
        /// This method returns the same instance of predicate on every call.
        /// </remarks>
        public static Func<I, O> Identity<I, O>()
            where I : O
            => Id<I, O>.Value;

        /// <summary>
        /// The converter which returns input argument
        /// without any modifications.
        /// </summary>
        /// <typeparam name="T">Type of input and output.</typeparam>
        /// <returns>The identity function.</returns>
        /// <remarks>
        /// This method returns the same instance of predicate on every call.
        /// </remarks>
        public static Func<T, T> Identity<T>() => Identity<T, T>();

        /// <summary>
        /// Converts <see cref="Func{T, Boolean}"/> into predicate.
        /// </summary>
        /// <typeparam name="T">Type of predicate argument.</typeparam>
        /// <param name="predicate">A delegate to convert.</param>
        /// <returns>A delegate of type <see cref="Predicate{T}"/> referencing the same method as original delegate.</returns>
        public static Predicate<T> AsPredicate<T>(this Func<T, bool> predicate)
            => predicate.ChangeType<Predicate<T>>();

        /// <summary>
        /// Converts <see cref="Func{I, O}"/> into <see cref="Converter{I, O}"/>.
        /// </summary>
        /// <typeparam name="I">Type of input argument.</typeparam>
        /// <typeparam name="O">Return type of the converter.</typeparam>
        /// <param name="function">The function to convert.</param>
        /// <returns>A delegate of type <see cref="Converter{I, O}"/> referencing the same method as original delegate.</returns>
        public static Converter<I, O> AsConverter<I, O>(this Func<I, O> function)
            => function.ChangeType<Converter<I, O>>();
    }
}
