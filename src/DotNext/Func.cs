using System.Runtime.CompilerServices;

namespace DotNext;

/// <summary>
/// Provides extension methods for delegate <see cref="Func{TResult}"/> and
/// predefined functions.
/// </summary>
public static class Func
{
    /// <summary>
    /// Gets a predicate that can be used to check whether the specified object is of specific type.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <returns>The predicate instance.</returns>
    public static Func<object?, bool> IsTypeOf<T>() => ObjectExtensions.IsTypeOf<T>;

    /// <summary>
    /// Returns predicate implementing nullability check.
    /// </summary>
    /// <typeparam name="T">Type of predicate argument.</typeparam>
    /// <returns>The predicate instance.</returns>
    /// <remarks>
    /// This method returns the same instance of predicate on every call.
    /// </remarks>
    public static Func<T, bool> IsNull<T>()
        where T : class?
        => ObjectExtensions.IsNull;

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
        where T : class?
        => ObjectExtensions.IsNotNull;

    /// <summary>
    /// The function which returns input argument
    /// without any modifications.
    /// </summary>
    /// <typeparam name="TInput">Type of input.</typeparam>
    /// <typeparam name="TOutput">Type of output.</typeparam>
    /// <returns>The identity function.</returns>
    /// <remarks>
    /// This method returns the same instance of predicate on every call.
    /// </remarks>
    public static Func<TInput, TOutput> Identity<TInput, TOutput>()
        where TInput : TOutput
        => ObjectExtensions.Identity<TInput, TOutput>;

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
    /// Represents <see cref="Action{T}"/> as <see cref="Func{T, TResult}"/> which doesn't modify the input value.
    /// </summary>
    /// <typeparam name="T">The type of the element.</typeparam>
    /// <param name="action">The action to be converted.</param>
    /// <returns><see cref="Func{T, TResult}"/> that wraps <paramref name="action"/>.</returns>
    public static Func<T, T> Identity<T>(this Action<T> action) => action.Identity;

    private static T Identity<T>(this Action<T> action, T item)
    {
        action(item);
        return item;
    }

    /// <summary>
    /// Constructs <see cref="Func{T}"/> returning the same
    /// instance each call.
    /// </summary>
    /// <param name="obj">The object to be returned from the delegate.</param>
    /// <typeparam name="T">The type of the object to be returned from the delegate.</typeparam>
    /// <returns>The delegate returning <paramref name="obj"/> each call.</returns>
    public static Func<T> Constant<T>(T obj)
    {
        // use cache for boolean values
        if (typeof(T) == typeof(bool))
            return Unsafe.As<Func<T>>(Constant(Unsafe.As<T, bool>(ref obj)));

        // cache nulls
        if (obj is null)
            return Default!;

        // slow path - allocates a new delegate
        unsafe
        {
            return DelegateHelpers.CreateDelegate<object?, T>(&ConstantCore, obj);
        }

        static T ConstantCore(object? obj) => (T)obj!;

        static T? Default() => default;
    }

    private static Func<bool> Constant(bool value)
    {
        return value ? True : False;

        static bool True() => true;

        static bool False() => false;
    }

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
    /// <typeparam name="TInput">Type of input argument.</typeparam>
    /// <typeparam name="TOutput">Return type of the converter.</typeparam>
    /// <param name="function">The function to convert.</param>
    /// <returns>A delegate of type <see cref="Converter{I, O}"/> referencing the same method as original delegate.</returns>
    public static Converter<TInput, TOutput> AsConverter<TInput, TOutput>(this Func<TInput, TOutput> function)
        => function.ChangeType<Converter<TInput, TOutput>>();

    /// <summary>
    /// Invokes function without throwing the exception.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="function">The function to invoke.</param>
    /// <returns>The invocation result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TResult> TryInvoke<TResult>(this Func<TResult> function)
    {
        Result<TResult> result;
        try
        {
            result = function();
        }
        catch (Exception e)
        {
            result = new(e);
        }

        return result;
    }

    /// <summary>
    /// Invokes function without throwing the exception.
    /// </summary>
    /// <typeparam name="T">The type of the first function argument.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="function">The function to invoke.</param>
    /// <param name="arg">The first function argument.</param>
    /// <returns>The invocation result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TResult> TryInvoke<T, TResult>(this Func<T, TResult> function, T arg)
    {
        Result<TResult> result;
        try
        {
            result = function(arg);
        }
        catch (Exception e)
        {
            result = new(e);
        }

        return result;
    }

    /// <summary>
    /// Invokes function without throwing the exception.
    /// </summary>
    /// <typeparam name="T1">The type of the first function argument.</typeparam>
    /// <typeparam name="T2">The type of the second function argument.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="function">The function to invoke.</param>
    /// <param name="arg1">The first function argument.</param>
    /// <param name="arg2">The second function argument.</param>
    /// <returns>The invocation result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TResult> TryInvoke<T1, T2, TResult>(this Func<T1, T2, TResult> function, T1 arg1, T2 arg2)
    {
        Result<TResult> result;
        try
        {
            result = function(arg1, arg2);
        }
        catch (Exception e)
        {
            result = new(e);
        }

        return result;
    }

    /// <summary>
    /// Invokes function without throwing the exception.
    /// </summary>
    /// <typeparam name="T1">The type of the first function argument.</typeparam>
    /// <typeparam name="T2">The type of the second function argument.</typeparam>
    /// <typeparam name="T3">The type of the third function argument.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="function">The function to invoke.</param>
    /// <param name="arg1">The first function argument.</param>
    /// <param name="arg2">The second function argument.</param>
    /// <param name="arg3">The third function argument.</param>
    /// <returns>The invocation result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TResult> TryInvoke<T1, T2, T3, TResult>(this Func<T1, T2, T3, TResult> function, T1 arg1, T2 arg2, T3 arg3)
    {
        Result<TResult> result;
        try
        {
            result = function(arg1, arg2, arg3);
        }
        catch (Exception e)
        {
            result = new(e);
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
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="function">The function to invoke.</param>
    /// <param name="arg1">The first function argument.</param>
    /// <param name="arg2">The second function argument.</param>
    /// <param name="arg3">The third function argument.</param>
    /// <param name="arg4">The fourth function argument.</param>
    /// <returns>The invocation result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TResult> TryInvoke<T1, T2, T3, T4, TResult>(this Func<T1, T2, T3, T4, TResult> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        Result<TResult> result;
        try
        {
            result = function(arg1, arg2, arg3, arg4);
        }
        catch (Exception e)
        {
            result = new(e);
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
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="function">The function to invoke.</param>
    /// <param name="arg1">The first function argument.</param>
    /// <param name="arg2">The second function argument.</param>
    /// <param name="arg3">The third function argument.</param>
    /// <param name="arg4">The fourth function argument.</param>
    /// <param name="arg5">The fifth function argument.</param>
    /// <returns>The invocation result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TResult> TryInvoke<T1, T2, T3, T4, T5, TResult>(this Func<T1, T2, T3, T4, T5, TResult> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        Result<TResult> result;
        try
        {
            result = function(arg1, arg2, arg3, arg4, arg5);
        }
        catch (Exception e)
        {
            result = new(e);
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
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="function">The function to invoke.</param>
    /// <param name="arg1">The first function argument.</param>
    /// <param name="arg2">The second function argument.</param>
    /// <param name="arg3">The third function argument.</param>
    /// <param name="arg4">The fourth function argument.</param>
    /// <param name="arg5">The fifth function argument.</param>
    /// <param name="arg6">The sixth function argument.</param>
    /// <returns>The invocation result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TResult> TryInvoke<T1, T2, T3, T4, T5, T6, TResult>(this Func<T1, T2, T3, T4, T5, T6, TResult> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        Result<TResult> result;
        try
        {
            result = function(arg1, arg2, arg3, arg4, arg5, arg6);
        }
        catch (Exception e)
        {
            result = new(e);
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
    /// <typeparam name="TResult">The result type.</typeparam>
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
    public static Result<TResult> TryInvoke<T1, T2, T3, T4, T5, T6, T7, TResult>(this Func<T1, T2, T3, T4, T5, T6, T7, TResult> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        Result<TResult> result;
        try
        {
            result = function(arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        }
        catch (Exception e)
        {
            result = new(e);
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
    /// <typeparam name="TResult">The result type.</typeparam>
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
    public static Result<TResult> TryInvoke<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        Result<TResult> result;
        try
        {
            result = function(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }
        catch (Exception e)
        {
            result = new(e);
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
    /// <typeparam name="TResult">The result type.</typeparam>
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
    public static Result<TResult> TryInvoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        Result<TResult> result;
        try
        {
            result = function(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }
        catch (Exception e)
        {
            result = new(e);
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
    /// <typeparam name="TResult">The result type.</typeparam>
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
    public static Result<TResult> TryInvoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> function, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        Result<TResult> result;
        try
        {
            result = function(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        }
        catch (Exception e)
        {
            result = new(e);
        }

        return result;
    }
}