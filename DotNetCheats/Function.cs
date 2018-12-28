namespace MissingPieces
{
    /// <summary>
    /// Represents a static function with arbitrary number of arguments
    /// allocated on the stack.
    /// </summary>
    /// <param name="arguments">Function arguments in the form of public structure fields.</param>
    /// <typeparam name="A">Type of structure with function arguments allocated on the stack.</typeparam>
    /// <typeparam name="R">Type of function return value.</typeparam>
    /// <returns>Function return value.</returns>
    public delegate R Function<A, R>(in A arguments)
        where A: struct;

    /// <summary>
    /// Represents an instance function with arbitrary number of arguments
    /// allocated on the stack.
    /// </summary>
    /// <param name="this">Hidden This parameter.</param>
    /// <param name="arguments">Function arguments in the form of public structure fields.</param>
    /// <typeparam name="T">Type of instance to be passed into underlying method.</typeparam>
    /// <typeparam name="A">Type of structure with function arguments allocated on the stack.</typeparam>
    /// <typeparam name="R">Type of function return value.</typeparam>
    /// <returns>Function return value.</returns>
    public delegate R Function<T, A, R>(in T @this, in A arguments);
}