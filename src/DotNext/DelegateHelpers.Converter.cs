namespace DotNext;

partial class DelegateHelpers
{
    /// <summary>
    /// Represents extensions for <see cref="Converter{TInput, TOutput}"/> type.
    /// </summary>
    /// <param name="converter">The converter to extend.</param>
    /// <typeparam name="TInput">The type of input argument.</typeparam>
    /// <typeparam name="TOutput">Return type of the function.</typeparam>
    extension<TInput, TOutput>(Converter<TInput, TOutput> converter)
        where TInput : allows ref struct
        where TOutput : allows ref struct
    {
        /// <summary>
        /// Converts <see cref="Converter{I, O}"/> into <see cref="Func{I, O}"/>.
        /// </summary>
        /// <returns>A delegate of type <see cref="Func{I, O}"/> referencing the same method as original delegate.</returns>
        public Func<TInput, TOutput> AsFunc()
            => converter.ChangeType<Func<TInput, TOutput>>();
    }

    /// <summary>
    /// Represents extensions for <see cref="Converter{TInput, TOutput}"/> type.
    /// </summary>
    /// <param name="converter">The converter to extend.</param>
    /// <typeparam name="TInput">The input type.</typeparam>
    extension<TInput>(Converter<TInput, bool> converter)
        where TInput : allows ref struct
    {
        /// <summary>
        /// Converts <see cref="Converter{T, Boolean}"/> into predicate.
        /// </summary>
        /// <returns>A delegate of type <see cref="Predicate{T}"/> referencing the same method as original delegate.</returns>
        public Predicate<TInput> AsPredicate()
            => converter.ChangeType<Predicate<TInput>>();
    }

    /// <summary>
    /// Represents extensions for <see cref="Converter{TInput, TOutput}"/> type.
    /// </summary>
    /// <param name="converter">The converter to extend.</param>
    /// <typeparam name="TInput">The type of input argument.</typeparam>
    /// <typeparam name="TOutput">Return type of the function.</typeparam>
    extension<TInput, TOutput>(Converter<TInput, TOutput> converter)
        where TInput : allows ref struct
    {
        /// <summary>
        /// Converts the input value without throwing exception.
        /// </summary>
        /// <param name="input">The input value to be converted.</param>
        /// <returns>The conversion result.</returns>
        public Result<TOutput> TryInvoke(TInput input)
        {
            Result<TOutput> result;
            try
            {
                result = converter(input);
            }
            catch (Exception e)
            {
                result = new(e);
            }

            return result;
        }
    }
}