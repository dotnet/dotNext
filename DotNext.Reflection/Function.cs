using System;

namespace DotNext
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
    /// <param name="this">Hidden <see langword="this"/> parameter.</param>
    /// <param name="arguments">Function arguments in the form of public structure fields.</param>
    /// <typeparam name="T">Type of instance to be passed into underlying method.</typeparam>
    /// <typeparam name="A">Type of structure with function arguments allocated on the stack.</typeparam>
    /// <typeparam name="R">Type of function return value.</typeparam>
    /// <returns>Function return value.</returns>
    public delegate R Function<T, A, R>(in T @this, in A arguments);

    public static class Function
    {
        public static A ArgList<A, R>(this Function<A, R> function)
            where A: struct
            => new A();
        
        public static A ArgList<T, A, R>(this Function<T, A, R> function)
            where A: struct
            => new A();

        public static R Invoke<P, R>(this Function<ValueTuple<P>, R> function, P arg)
            => function(new ValueTuple<P>(arg));

        public static R Invoke<T, P, R>(this Function<T, ValueTuple<P>, R> function, in T instance, P arg)
            => function(in instance, new ValueTuple<P>(arg));
        
    }
}