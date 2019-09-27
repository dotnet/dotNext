namespace DotNext.Diagnostics
{
    /// <summary>
    /// Represents runtime assertions.
    /// </summary>
    public static class Assert
    {
        /// <summary>
        /// Checks for a condition; if condition is <see langword="false"/> then throw <see cref="AssertionException"/>.
        /// </summary>
        /// <param name="condition">The conditional expression to evaluate.</param>
        /// <param name="message">The message to display.</param>
        /// <exception cref="AssertionException">Check failed.</exception>
        public static void True(bool condition, string message = "")
        {
            if (!condition)
                throw new AssertionException(message);
        }

        /// <summary>
        /// Checks for a condition; if condition is <see langword="true"/> then throw <see cref="AssertionException"/>.
        /// </summary>
        /// <param name="condition">The conditional expression to evaluate.</param>
        /// <param name="message">The message to display.</param>
        /// <exception cref="AssertionException">Check failed.</exception>
        public static void False(bool condition, string message = "")
        {
            if (condition)
                throw new AssertionException(message);
        }
    }
}