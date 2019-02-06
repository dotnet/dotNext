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
        
        public static R Invoke<T, R>(this Function<T, ValueTuple, R> function, in T instance)
			=> function(in instance, in EmptyTuple.Value);
		
		public static R Invoke<R>(this Function<ValueTuple, R> function)
			=> function(in EmptyTuple.Value);

        public static R Invoke<P, R>(this Function<ValueTuple<P>, R> function, P arg)
            => function(new ValueTuple<P>(arg));

        public static R Invoke<T, P, R>(this Function<T, ValueTuple<P>, R> function, in T instance, P arg)
            => function(in instance, new ValueTuple<P>(arg));
        
        public static R Invoke<P1, P2, R>(this Function<(P1, P2), R> function, P1 arg1, P2 arg2)
            => function((arg1, arg2));

        public static R Invoke<T, P1, P2, R>(this Function<T, (P1, P2), R> function, in T instance, P1 arg1, P2 arg2)
            => function(in instance, (arg1, arg2));
        
        public static R Invoke<T, P1, P2, P3, R>(this Function<T, (P1, P2, P3), R> function, in T instance, P1 arg1, P2 arg2, P3 arg3)
            => function(in instance, (arg1, arg2, arg3));

        public static R Invoke<P1, P2, P3, R>(this Function<(P1, P2, P3), R> function, P1 arg1, P2 arg2, P3 arg3)
            => function((arg1, arg2, arg3));
        
        public static R Invoke<T, P1, P2, P3, P4, R>(this Function<T, (P1, P2, P3, P4), R> function, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4)
            => function(in instance, (arg1, arg2, arg3, arg4));

        public static R Invoke<P1, P2, P3, P4, R>(this Function<(P1, P2, P3, P4), R> function, P1 arg1, P2 arg2, P3 arg3, P4 arg4)
            => function((arg1, arg2, arg3, arg4));
        
        public static R Invoke<T, P1, P2, P3, P4, P5, R>(this Function<T, (P1, P2, P3, P4, P5), R> function, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5)
            => function(in instance, (arg1, arg2, arg3, arg4, arg5));
        
        public static R Invoke<P1, P2, P3, P4, P5, R>(this Function<(P1, P2, P3, P4, P5), R> function, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5)
            => function((arg1, arg2, arg3, arg4, arg5));
        
        public static R Invoke<T, P1, P2, P3, P4, P5, P6, R>(this Function<T, (P1, P2, P3, P4, P5, P6), R> function, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6)
            => function(in instance, (arg1, arg2, arg3, arg4, arg5, arg6));

        public static R Invoke<P1, P2, P3, P4, P5, P6, R>(this Function<(P1, P2, P3, P4, P5, P6), R> function, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6)
            => function((arg1, arg2, arg3, arg4, arg5, arg6));
        
        public static R Invoke<T, P1, P2, P3, P4, P5, P6, P7, R>(this Function<T, (P1, P2, P3, P4, P5, P6, P7), R> function, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7)
            => function(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7));
        
        public static R Invoke<P1, P2, P3, P4, P5, P6, P7, R>(this Function<(P1, P2, P3, P4, P5, P6, P7), R> function, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7)
            => function((arg1, arg2, arg3, arg4, arg5, arg6, arg7));
        
        public static R Invoke<T, P1, P2, P3, P4, P5, P6, P7, P8, R>(this Function<T, (P1, P2, P3, P4, P5, P6, P7, P8), R> function, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8)
            => function(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));
        
        public static R Invoke<P1, P2, P3, P4, P5, P6, P7, P8, R>(this Function<(P1, P2, P3, P4, P5, P6, P7, P8), R> function, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8)
            => function((arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));
        
        public static R Invoke<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, R>(this Function<T, (P1, P2, P3, P4, P5, P6, P7, P8, P9), R> function, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9)
            => function(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));
        
        public static R Invoke<P1, P2, P3, P4, P5, P6, P7, P8, P9, R>(this Function<(P1, P2, P3, P4, P5, P6, P7, P8, P9), R> function, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9)
            => function((arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));
        
        public static R Invoke<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, R>(this Function<T, (P1, P2, P3, P4, P5, P6, P7, P8, P9, P10), R> function, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9, P10 arg10)
            => function(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));
        
        public static R Invoke<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, R>(this Function<(P1, P2, P3, P4, P5, P6, P7, P8, P9, P10), R> function, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9, P10 arg10)
            => function((arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));
    }
}