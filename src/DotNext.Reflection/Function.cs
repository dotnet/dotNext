using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext
{
    /// <summary>
    /// Represents a static function with arbitrary number of arguments
    /// allocated on the stack.
    /// </summary>
    /// <param name="arguments">Function arguments in the form of public structure fields.</param>
    /// <typeparam name="TArgs">Type of structure with function arguments allocated on the stack.</typeparam>
    /// <typeparam name="TResult">Type of function return value.</typeparam>
    /// <returns>Function return value.</returns>
    public delegate TResult? Function<TArgs, out TResult>(in TArgs arguments)
        where TArgs : struct;

    /// <summary>
    /// Represents an instance function with arbitrary number of arguments
    /// allocated on the stack.
    /// </summary>
    /// <param name="this">Hidden <c>this</c> parameter.</param>
    /// <param name="arguments">Function arguments in the form of public structure fields.</param>
    /// <typeparam name="T">Type of instance to be passed into underlying method.</typeparam>
    /// <typeparam name="TArgs">Type of structure with function arguments allocated on the stack.</typeparam>
    /// <typeparam name="TResult">Type of function return value.</typeparam>
    /// <returns>Function return value.</returns>
    public delegate TResult? Function<T, TArgs, out TResult>([DisallowNull] in T @this, in TArgs arguments)
        where TArgs : struct;

    /// <summary>
    /// Provides extension methods for delegates <see cref="Function{A, R}"/> and <see cref="Function{T, A, R}"/>.
    /// </summary>
    public static class Function
    {
        private sealed class Closure<T, TArgs, TResult>
            where TArgs : struct
        {
            private readonly Function<T, TArgs, TResult> function;
            [NotNull]
            private readonly T target;

            internal Closure(Function<T, TArgs, TResult> function, [DisallowNull] T target)
            {
                this.function = function;
                this.target = target;
            }

            internal TResult? Invoke(in TArgs arguments) => function(target, arguments);
        }

        /// <summary>
        /// Converts <see cref="Function{T, A, R}"/> into <see cref="Function{A, R}"/> through
        /// capturing of the first argument of <see cref="Function{T, A, R}"/> delegate.
        /// </summary>
        /// <typeparam name="T">Type of instance to be passed into underlying method.</typeparam>
        /// <typeparam name="TArgs">Type of structure with function arguments allocated on the stack.</typeparam>
        /// <typeparam name="TResult">Type of function return value.</typeparam>
        /// <param name="function">The function to be converted.</param>
        /// <param name="this">The first argument to be captured.</param>
        /// <returns>The function instance.</returns>
        public static Function<TArgs, TResult> Bind<T, TArgs, TResult>(this Function<T, TArgs, TResult> function, [DisallowNull] T @this)
            where TArgs : struct
            => new Closure<T, TArgs, TResult>(function, @this).Invoke;

        /// <summary>
        /// Invokes function without throwing exception in case of its failure.
        /// </summary>
        /// <param name="function">The function to invoke.</param>
        /// <param name="arguments">Function arguments in the form of public structure fields.</param>
        /// <typeparam name="TArgs">Type of structure with function arguments allocated on the stack.</typeparam>
        /// <typeparam name="TResult">Type of function return value.</typeparam>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<TResult?> TryInvoke<TArgs, TResult>(this Function<TArgs, TResult> function, in TArgs arguments)
            where TArgs : struct
        {
            try
            {
                return function(in arguments);
            }
            catch (Exception e)
            {
                return new Result<TResult?>(e);
            }
        }

        /// <summary>
        /// Invokes function without throwing exception in case of its failure.
        /// </summary>
        /// <param name="function">The function to invoke.</param>
        /// <param name="this">Hidden <c>this</c> parameter.</param>
        /// <param name="arguments">Function arguments in the form of public structure fields.</param>
        /// <typeparam name="T">Type of instance to be passed into underlying method.</typeparam>
        /// <typeparam name="TArgs">Type of structure with function arguments allocated on the stack.</typeparam>
        /// <typeparam name="TResult">Type of function return value.</typeparam>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<TResult?> TryInvoke<T, TArgs, TResult>(this Function<T, TArgs, TResult> function, [DisallowNull] in T @this, in TArgs arguments)
            where TArgs : struct
        {
            try
            {
                return function(in @this, in arguments);
            }
            catch (Exception e)
            {
                return new Result<TResult?>(e);
            }
        }

        /// <summary>
        /// Allocates list of arguments on the stack.
        /// </summary>
        /// <typeparam name="TArgs">The type representing list of arguments.</typeparam>
        /// <typeparam name="TResult">The return type of the function.</typeparam>
        /// <param name="function">The function instance.</param>
        /// <returns>Allocated list of arguments.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TArgs ArgList<TArgs, TResult>(this Function<TArgs, TResult> function)
            where TArgs : struct
            => new();

        /// <summary>
        /// Allocates list of arguments on the stack.
        /// </summary>
        /// <typeparam name="T">Type of explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="TArgs">The type representing list of arguments.</typeparam>
        /// <typeparam name="TResult">The return type of the function.</typeparam>
        /// <param name="function">The function instance.</param>
        /// <returns>Allocated list of arguments.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TArgs ArgList<T, TArgs, TResult>(this Function<T, TArgs, TResult> function)
            where TArgs : struct
            => new();

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T, TResult>(this Function<T, ValueTuple, TResult> function, [DisallowNull] in T instance)
            => function(in instance, default);

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<TResult>(this Function<ValueTuple, TResult> function)
            => function(default);

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="TParam">The type of the first function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="arg">The first function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<TParam, TResult>(this Function<ValueTuple<TParam>, TResult> function, TParam arg)
            => function(new ValueTuple<TParam>(arg));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="TParam">The type of the first function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg">The first function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T, TParam, TResult>(this Function<T, ValueTuple<TParam>, TResult> function, [DisallowNull] in T instance, TParam arg)
            => function(in instance, new ValueTuple<TParam>(arg));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T1, T2, TResult>(this Function<(T1, T2), TResult> function, T1 arg1, T2 arg2)
            => function((arg1, arg2));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T, T1, T2, TResult>(this Function<T, (T1, T2), TResult> function, [DisallowNull] in T instance, T1 arg1, T2 arg2)
            => function(in instance, (arg1, arg2));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T, T1, T2, T3, TResult>(this Function<T, (T1, T2, T3), TResult> function, [DisallowNull] in T instance, T1 arg1, T2 arg2, T3 arg3)
            => function(in instance, (arg1, arg2, arg3));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="TParam1">The type of the first function argument.</typeparam>
        /// <typeparam name="TParam2">The type of the second function argument.</typeparam>
        /// <typeparam name="TParam3">The type of the third function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<TParam1, TParam2, TParam3, TResult>(this Function<(TParam1, TParam2, TParam3), TResult> function, TParam1 arg1, TParam2 arg2, TParam3 arg3)
            => function((arg1, arg2, arg3));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c>.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T, T1, T2, T3, T4, TResult>(this Function<T, (T1, T2, T3, T4), TResult> function, [DisallowNull] in T instance, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => function(in instance, (arg1, arg2, arg3, arg4));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T1, T2, T3, T4, TResult>(this Function<(T1, T2, T3, T4), TResult> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => function((arg1, arg2, arg3, arg4));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c>.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c>.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T, T1, T2, T3, T4, T5, TResult>(this Function<T, (T1, T2, T3, T4, T5), TResult> function, [DisallowNull] in T instance, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => function(in instance, (arg1, arg2, arg3, arg4, arg5));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T1, T2, T3, T4, T5, TResult>(this Function<(T1, T2, T3, T4, T5), TResult> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => function((arg1, arg2, arg3, arg4, arg5));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T, T1, T2, T3, T4, T5, T6, TResult>(this Function<T, (T1, T2, T3, T4, T5, T6), TResult> function, [DisallowNull] in T instance, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            => function(in instance, (arg1, arg2, arg3, arg4, arg5, arg6));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T1, T2, T3, T4, T5, T6, TResult>(this Function<(T1, T2, T3, T4, T5, T6), TResult> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            => function((arg1, arg2, arg3, arg4, arg5, arg6));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T, T1, T2, T3, T4, T5, T6, T7, TResult>(this Function<T, (T1, T2, T3, T4, T5, T6, T7), TResult> function, [DisallowNull] in T instance, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
            => function(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T1, T2, T3, T4, T5, T6, T7, TResult>(this Function<(T1, T2, T3, T4, T5, T6, T7), TResult> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
            => function((arg1, arg2, arg3, arg4, arg5, arg6, arg7));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <param name="arg8">The eighth function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T, T1, T2, T3, T4, T5, T6, T7, T8, TResult>(this Function<T, (T1, T2, T3, T4, T5, T6, T7, T8), TResult> function, [DisallowNull] in T instance, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
            => function(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <param name="arg8">The eighth function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(this Function<(T1, T2, T3, T4, T5, T6, T7, T8), TResult> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
            => function((arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth function argument.</typeparam>
        /// <typeparam name="T9">The type of the ninth function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <param name="arg8">The eighth function argument.</param>
        /// <param name="arg9">The ninth function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T, T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>(this Function<T, (T1, T2, T3, T4, T5, T6, T7, T8, T9), TResult> function, [DisallowNull] in T instance, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
            => function(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth function argument.</typeparam>
        /// <typeparam name="T9">The type of the ninth function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <param name="arg8">The eighth function argument.</param>
        /// <param name="arg9">The ninth function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>(this Function<(T1, T2, T3, T4, T5, T6, T7, T8, T9), TResult> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
            => function((arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth function argument.</typeparam>
        /// <typeparam name="T9">The type of the ninth function argument.</typeparam>
        /// <typeparam name="T10">The type of the tenth function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <param name="arg8">The eighth function argument.</param>
        /// <param name="arg9">The ninth function argument.</param>
        /// <param name="arg10">The tenth function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(this Function<T, (T1, T2, T3, T4, T5, T6, T7, T8, T9, T10), TResult> function, [DisallowNull] in T instance, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
            => function(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));

        /// <summary>
        /// Invokes function.
        /// </summary>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth function argument.</typeparam>
        /// <typeparam name="T9">The type of the ninth function argument.</typeparam>
        /// <typeparam name="T10">The type of the tenth function argument.</typeparam>
        /// <typeparam name="TResult">The type of function return value.</typeparam>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <param name="arg8">The eighth function argument.</param>
        /// <param name="arg9">The ninth function argument.</param>
        /// <param name="arg10">The tenth function argument.</param>
        /// <returns>Function return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(this Function<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10), TResult> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
            => function((arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));
    }
}