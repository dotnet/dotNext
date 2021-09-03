namespace DotNext.Reflection
{
    /// <summary>
    /// Represents operator resolution strategy.
    /// </summary>
    public enum OperatorLookup : byte
    {
        /// <summary>
        /// Check for predefined operator only.
        /// </summary>
        Predefined = 0,

        /// <summary>
        /// Check for user-defined (overloaded) operator only.
        /// </summary>
        Overloaded = 1,

        /// <summary>
        /// Check for any operator.
        /// </summary>
        Any = 2,
    }
}