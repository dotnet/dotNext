namespace DotNext
{
    /// <summary>
    /// Represents conditional action.
    /// </summary>
    /// <typeparam name="T">The type of the context.</typeparam>
    public interface IConditionalAction<in T>
    {
        /// <summary>
        /// Tests whether the condition is met.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>
        /// <see langword="true"/> to execute <see cref="Execute(T)"/>;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        bool Test(T obj);

        /// <summary>
        /// Executes action if the condition is met.
        /// </summary>
        /// <param name="obj">The argument of the action.</param>
        void Execute(T obj);
    }
}