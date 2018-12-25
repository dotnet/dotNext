namespace MissingPieces.Metaprogramming
{
    /// <summary>
    /// Represents a function with arbitrary number of arguments
    /// allocated on the stack.
    /// </summary>
    /// <param name="arguments">Function arguments in the form of public structure fields.</param>
    /// <typeparam name="A">Type of structure with function arguments allocated on the stack.</typeparam>
    /// <typeparam name="R">Type of function return value.</typeparam>
    /// <returns>Function return value.</returns>
    public delegate R Function<A, R>(in A arguments)
        where A: struct;
}