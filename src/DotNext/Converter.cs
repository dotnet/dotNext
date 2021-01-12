using System;
using System.Runtime.CompilerServices;

namespace DotNext
{
    /// <summary>
    /// Provides extension methods for delegate <see cref="Converter{TInput, TOutput}"/> and
    /// predefined converters.
    /// </summary>
    public static class Converter
    {
        private static class Id<TInput, TOutput>
            where TInput : TOutput
        {
            internal static readonly Converter<TInput, TOutput> Value = Identity<TInput, TOutput>;
        }

        internal static TOutput Identity<TInput, TOutput>(TInput input)
            where TInput : TOutput => input;

        /// <summary>
        /// The converter which returns input argument
        /// without any modifications.
        /// </summary>
        /// <typeparam name="TInput">Type of input.</typeparam>
        /// <typeparam name="TOutput">Type of output.</typeparam>
        /// <returns>The identity function.</returns>
        /// <remarks>
        /// This method returns the same instance of predicate on every call.
        /// </remarks>
        public static Converter<TInput, TOutput> Identity<TInput, TOutput>()
            where TInput : TOutput
            => Id<TInput, TOutput>.Value;

        /// <summary>
        /// The converter which returns input argument
        /// without any modifications.
        /// </summary>
        /// <typeparam name="T">The type of input and output.</typeparam>
        /// <returns>The identity function.</returns>
        /// <remarks>
        /// This method returns the same instance of predicate on every call.
        /// </remarks>
        public static Converter<T, T> Identity<T>() => Identity<T, T>();

        /// <summary>
        /// Converts <see cref="Converter{I, O}"/> into <see cref="Func{I, O}"/>.
        /// </summary>
        /// <typeparam name="TInput">The type of input argument.</typeparam>
        /// <typeparam name="TOutput">Return type of the function.</typeparam>
        /// <param name="converter">The converted delegate.</param>
        /// <returns>A delegate of type <see cref="Func{I, O}"/> referencing the same method as original delegate.</returns>
        public static Func<TInput, TOutput> AsFunc<TInput, TOutput>(this Converter<TInput, TOutput> converter)
            => converter.ChangeType<Func<TInput, TOutput>>();

        /// <summary>
        /// Converts <see cref="Converter{T, Boolean}"/> into predicate.
        /// </summary>
        /// <typeparam name="T">Type of predicate argument.</typeparam>
        /// <param name="converter">A delegate to convert.</param>
        /// <returns>A delegate of type <see cref="Predicate{T}"/> referencing the same method as original delegate.</returns>
        public static Predicate<T> AsPredicate<T>(this Converter<T, bool> converter)
            => converter.ChangeType<Predicate<T>>();

        /// <summary>
        /// Converts the input value without throwing exception.
        /// </summary>
        /// <param name="converter">The converter to invoke.</param>
        /// <param name="input">The input value to be converted.</param>
        /// <typeparam name="TInput">The type of the value to be converted.</typeparam>
        /// <typeparam name="TOutput">The type of the conversion result.</typeparam>
        /// <returns>The conversion result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<TOutput> TryInvoke<TInput, TOutput>(this Converter<TInput, TOutput> converter, TInput input)
        {
            Result<TOutput> result;
            try
            {
                result = converter(input);
            }
            catch (Exception e)
            {
                result = new Result<TOutput>(e);
            }

            return result;
        }

        /// <summary>
        /// Converts <see cref="Converter{TInput, TOutput}"/> into <see cref="ValueFunc{T, R}"/>.
        /// </summary>
        /// <typeparam name="TInput">The type of object that is to be converted.</typeparam>
        /// <typeparam name="TOutput">The result of conversion.</typeparam>
        /// <param name="converter">The type the input object is to be converted to.</param>
        /// <returns>The value delegate representing converter.</returns>
        public static ValueFunc<TInput, TOutput> AsValueFunc<TInput, TOutput>(this Converter<TInput, TOutput> converter)
            => new ValueFunc<TInput, TOutput>(Unsafe.As<Func<TInput, TOutput>>(converter));
    }
}
