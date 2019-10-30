using System;
using System.Runtime.CompilerServices;

namespace DotNext
{
    /// <summary>
    /// Provides extension methods for delegate <see cref="Func{TResult}"/> and
    /// predefined functions.
    /// </summary>
    public static class Func
    {
        private static class Id<I, O>
            where I : O
        {
            internal static readonly Func<I, O> Value = Converter.Identity<I, O>;
        }

        private static class IsNullFunc<T>
            where T : class
        {
            internal static readonly Func<T, bool> Value = ObjectExtensions.IsNull;
        }

        private static class IsNotNullFunc<T>
            where T : class
        {
            internal static readonly Func<T, bool> Value = ObjectExtensions.IsNotNull;
        }

        /// <summary>
        /// Returns predicate implementing nullability check.
        /// </summary>
        /// <typeparam name="T">Type of predicate argument.</typeparam>
        /// <returns>The predicate instance.</returns>
        /// <remarks>
        /// This method returns the same instance of predicate on every call.
        /// </remarks>
        public static Func<T, bool> IsNull<T>()
            where T : class
            => IsNullFunc<T>.Value;

        /// <summary>
        /// Returns predicate checking that input argument 
        /// is not <see langword="null"/>.
        /// </summary>
        /// <typeparam name="T">Type of the predicate argument.</typeparam>
        /// <returns>The predicate instance.</returns>
        /// <remarks>
        /// This method returns the same instance of predicate on every call.
        /// </remarks>
        public static Func<T, bool> IsNotNull<T>()
            where T : class
            => IsNotNullFunc<T>.Value;

        /// <summary>
        /// The function which returns input argument
        /// without any modifications.
        /// </summary>
        /// <typeparam name="I">Type of input.</typeparam>
        /// <typeparam name="O">Type of output.</typeparam>
        /// <returns>The identity function.</returns>
        /// <remarks>
        /// This method returns the same instance of predicate on every call.
        /// </remarks>
        public static Func<I, O> Identity<I, O>()
            where I : O
            => Id<I, O>.Value;

        /// <summary>
        /// The converter which returns input argument
        /// without any modifications.
        /// </summary>
        /// <typeparam name="T">Type of input and output.</typeparam>
        /// <returns>The identity function.</returns>
        /// <remarks>
        /// This method returns the same instance of predicate on every call.
        /// </remarks>
        public static Func<T, T> Identity<T>() => Identity<T, T>();

        /// <summary>
        /// Converts <see cref="Func{T, Boolean}"/> into predicate.
        /// </summary>
        /// <typeparam name="T">Type of predicate argument.</typeparam>
        /// <param name="predicate">A delegate to convert.</param>
        /// <returns>A delegate of type <see cref="Predicate{T}"/> referencing the same method as original delegate.</returns>
        public static Predicate<T> AsPredicate<T>(this Func<T, bool> predicate)
            => predicate.ChangeType<Predicate<T>>();

        /// <summary>
        /// Converts <see cref="Func{I, O}"/> into <see cref="Converter{I, O}"/>.
        /// </summary>
        /// <typeparam name="I">Type of input argument.</typeparam>
        /// <typeparam name="O">Return type of the converter.</typeparam>
        /// <param name="function">The function to convert.</param>
        /// <returns>A delegate of type <see cref="Converter{I, O}"/> referencing the same method as original delegate.</returns>
        public static Converter<I, O> AsConverter<I, O>(this Func<I, O> function)
            => function.ChangeType<Converter<I, O>>();

        /// <summary>
        /// Invokes function without throwing the exception.
        /// </summary>
        /// <typeparam name="R">The result type.</typeparam>
        /// <param name="function">The function to invoke.</param>
        /// <returns>The invocation result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<R> TryInvoke<R>(this Func<R> function)
        {
            Result<R> result;
            try
            {
                result = function();
            }
            catch (Exception e)
            {
                result = new Result<R>(e);
            }
            return result;
        }

        /// <summary>
        /// Invokes function without throwing the exception.
        /// </summary>
        /// <typeparam name="T">The type of the first function argument.</typeparam>
        /// <typeparam name="R">The result type.</typeparam>
        /// <param name="function">The function to invoke.</param>
        /// <param name="arg">The first function argument.</param>
        /// <returns>The invocation result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<R> TryInvoke<T, R>(this Func<T, R> function, T arg)
        {
            Result<R> result;
            try
            {
                result = function(arg);
            }
            catch (Exception e)
            {
                result = new Result<R>(e);
            }
            return result;
        }

        /// <summary>
        /// Invokes function without throwing the exception.
        /// </summary>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="R">The result type.</typeparam>
        /// <param name="function">The function to invoke.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <returns>The invocation result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<R> TryInvoke<T1, T2, R>(this Func<T1, T2, R> function, T1 arg1, T2 arg2)
        {
            Result<R> result;
            try
            {
                result = function(arg1, arg2);
            }
            catch (Exception e)
            {
                result = new Result<R>(e);
            }
            return result;
        }

        /// <summary>
        /// Invokes function without throwing the exception.
        /// </summary>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="R">The result type.</typeparam>
        /// <param name="function">The function to invoke.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <returns>The invocation result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<R> TryInvoke<T1, T2, T3, R>(this Func<T1, T2, T3, R> function, T1 arg1, T2 arg2, T3 arg3)
        {
            Result<R> result;
            try
            {
                result = function(arg1, arg2, arg3);
            }
            catch (Exception e)
            {
                result = new Result<R>(e);
            }
            return result;
        }

        /// <summary>
        /// Invokes function without throwing the exception.
        /// </summary>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="R">The result type.</typeparam>
        /// <param name="function">The function to invoke.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <returns>The invocation result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<R> TryInvoke<T1, T2, T3, T4, R>(this Func<T1, T2, T3, T4, R> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            Result<R> result;
            try
            {
                result = function(arg1, arg2, arg3, arg4);
            }
            catch (Exception e)
            {
                result = new Result<R>(e);
            }
            return result;
        }

        /// <summary>
        /// Invokes function without throwing the exception.
        /// </summary>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="R">The result type.</typeparam>
        /// <param name="function">The function to invoke.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <returns>The invocation result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<R> TryInvoke<T1, T2, T3, T4, T5, R>(this Func<T1, T2, T3, T4, T5, R> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            Result<R> result;
            try
            {
                result = function(arg1, arg2, arg3, arg4, arg5);
            }
            catch (Exception e)
            {
                result = new Result<R>(e);
            }
            return result;
        }

        /// <summary>
        /// Invokes function without throwing the exception.
        /// </summary>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="R">The result type.</typeparam>
        /// <param name="function">The function to invoke.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <returns>The invocation result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<R> TryInvoke<T1, T2, T3, T4, T5, T6, R>(this Func<T1, T2, T3, T4, T5, T6, R> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            Result<R> result;
            try
            {
                result = function(arg1, arg2, arg3, arg4, arg5, arg6);
            }
            catch (Exception e)
            {
                result = new Result<R>(e);
            }
            return result;
        }

        /// <summary>
        /// Invokes function without throwing the exception.
        /// </summary>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="R">The result type.</typeparam>
        /// <param name="function">The function to invoke.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <returns>The invocation result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<R> TryInvoke<T1, T2, T3, T4, T5, T6, T7, R>(this Func<T1, T2, T3, T4, T5, T6, T7, R> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
        {
            Result<R> result;
            try
            {
                result = function(arg1, arg2, arg3, arg4, arg5, arg6, arg7);
            }
            catch (Exception e)
            {
                result = new Result<R>(e);
            }
            return result;
        }

        /// <summary>
        /// Invokes function without throwing the exception.
        /// </summary>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth function argument.</typeparam>
        /// <typeparam name="R">The result type.</typeparam>
        /// <param name="function">The function to invoke.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <param name="arg8">The eighth function argument.</param>
        /// <returns>The invocation result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<R> TryInvoke<T1, T2, T3, T4, T5, T6, T7, T8, R>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, R> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
        {
            Result<R> result;
            try
            {
                result = function(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
            }
            catch (Exception e)
            {
                result = new Result<R>(e);
            }
            return result;
        }

        /// <summary>
        /// Invokes function without throwing the exception.
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
        /// <typeparam name="R">The result type.</typeparam>
        /// <param name="function">The function to invoke.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <param name="arg8">The eighth function argument.</param>
        /// <param name="arg9">The ninth function argument.</param>
        /// <returns>The invocation result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<R> TryInvoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, R>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, R> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
        {
            Result<R> result;
            try
            {
                result = function(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
            }
            catch (Exception e)
            {
                result = new Result<R>(e);
            }
            return result;
        }

        /// <summary>
        /// Invokes function without throwing the exception.
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
        /// <typeparam name="R">The result type.</typeparam>
        /// <param name="function">The function to invoke.</param>
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
        /// <returns>The invocation result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<R> TryInvoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, R>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, R> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
        {
            Result<R> result;
            try
            {
                result = function(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
            }
            catch (Exception e)
            {
                result = new Result<R>(e);
            }
            return result;
        }
    }
}
