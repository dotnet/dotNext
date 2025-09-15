namespace DotNext;

public partial class DelegateHelpers
{
    /// <summary>
    /// Invokes the action without throwing the exception.
    /// </summary>
    /// <param name="action">The action to be invoked.</param>
    /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
    public static Exception? TryInvoke(this Action action)
    {
        var result = default(Exception);
        try
        {
            action();
        }
        catch (Exception e)
        {
            result = e;
        }

        return result;
    }
    
    /// <summary>
    /// Invokes the action without throwing the exception.
    /// </summary>
    /// <typeparam name="T">The type of the first action argument.</typeparam>
    /// <param name="action">The action to be invoked.</param>
    /// <param name="arg">The first action argument.</param>
    /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
    public static Exception? TryInvoke<T>(this Action<T> action, T arg)
    {
        var result = default(Exception);
        try
        {
            action(arg);
        }
        catch (Exception e)
        {
            result = e;
        }

        return result;
    }

    /// <summary>
    /// Invokes the action without throwing the exception.
    /// </summary>
    /// <typeparam name="T1">The type of the first action argument.</typeparam>
    /// <typeparam name="T2">The type of the second action argument.</typeparam>
    /// <param name="action">The action to be invoked.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
    public static Exception? TryInvoke<T1, T2>(this Action<T1, T2> action, T1 arg1, T2 arg2)
    {
        var result = default(Exception);
        try
        {
            action(arg1, arg2);
        }
        catch (Exception e)
        {
            result = e;
        }

        return result;
    }
    
    /// <summary>
    /// Invokes the action without throwing the exception.
    /// </summary>
    /// <typeparam name="T1">The type of the first action argument.</typeparam>
    /// <typeparam name="T2">The type of the second action argument.</typeparam>
    /// <typeparam name="T3">The type of the third action argument.</typeparam>
    /// <param name="action">The action to be invoked.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <param name="arg3">The third action argument.</param>
    /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
    public static Exception? TryInvoke<T1, T2, T3>(this Action<T1, T2, T3> action, T1 arg1, T2 arg2, T3 arg3)
    {
        var result = default(Exception);
        try
        {
            action(arg1, arg2, arg3);
        }
        catch (Exception e)
        {
            result = e;
        }

        return result;
    }
    
    /// <summary>
    /// Invokes the action without throwing the exception.
    /// </summary>
    /// <typeparam name="T1">The type of the first action argument.</typeparam>
    /// <typeparam name="T2">The type of the second action argument.</typeparam>
    /// <typeparam name="T3">The type of the third action argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth action argument.</typeparam>
    /// <param name="action">The action to be invoked.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <param name="arg3">The third action argument.</param>
    /// <param name="arg4">The fourth action argument.</param>
    /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
    public static Exception? TryInvoke<T1, T2, T3, T4>(this Action<T1, T2, T3, T4> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        var result = default(Exception);
        try
        {
            action(arg1, arg2, arg3, arg4);
        }
        catch (Exception e)
        {
            result = e;
        }

        return result;
    }
    
    /// <summary>
    /// Invokes the action without throwing the exception.
    /// </summary>
    /// <typeparam name="T1">The type of the first action argument.</typeparam>
    /// <typeparam name="T2">The type of the second action argument.</typeparam>
    /// <typeparam name="T3">The type of the third action argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth action argument.</typeparam>
    /// <typeparam name="T5">The type of the fifth action argument.</typeparam>
    /// <param name="action">The action to be invoked.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <param name="arg3">The third action argument.</param>
    /// <param name="arg4">The fourth action argument.</param>
    /// <param name="arg5">The fifth action argument.</param>
    /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
    public static Exception? TryInvoke<T1, T2, T3, T4, T5>(this Action<T1, T2, T3, T4, T5> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        var result = default(Exception);
        try
        {
            action(arg1, arg2, arg3, arg4, arg5);
        }
        catch (Exception e)
        {
            result = e;
        }

        return result;
    }

    /// <summary>
    /// Invokes the action without throwing the exception.
    /// </summary>
    /// <typeparam name="T1">The type of the first action argument.</typeparam>
    /// <typeparam name="T2">The type of the second action argument.</typeparam>
    /// <typeparam name="T3">The type of the third action argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth action argument.</typeparam>
    /// <typeparam name="T5">The type of the fifth action argument.</typeparam>
    /// <typeparam name="T6">The type of the sixth action argument.</typeparam>
    /// <param name="action">The action to be invoked.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <param name="arg3">The third action argument.</param>
    /// <param name="arg4">The fourth action argument.</param>
    /// <param name="arg5">The fifth action argument.</param>
    /// <param name="arg6">The sixth action argument.</param>
    /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
    public static Exception? TryInvoke<T1, T2, T3, T4, T5, T6>(this Action<T1, T2, T3, T4, T5, T6> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4,
        T5 arg5, T6 arg6)
    {
        var result = default(Exception);
        try
        {
            action(arg1, arg2, arg3, arg4, arg5, arg6);
        }
        catch (Exception e)
        {
            result = e;
        }

        return result;
    }
    
    /// <summary>
    /// Invokes the action without throwing the exception.
    /// </summary>
    /// <typeparam name="T1">The type of the first action argument.</typeparam>
    /// <typeparam name="T2">The type of the second action argument.</typeparam>
    /// <typeparam name="T3">The type of the third action argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth action argument.</typeparam>
    /// <typeparam name="T5">The type of the fifth action argument.</typeparam>
    /// <typeparam name="T6">The type of the sixth action argument.</typeparam>
    /// <typeparam name="T7">The type of the seventh action argument.</typeparam>
    /// <param name="action">The action to be invoked.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <param name="arg3">The third action argument.</param>
    /// <param name="arg4">The fourth action argument.</param>
    /// <param name="arg5">The fifth action argument.</param>
    /// <param name="arg6">The sixth action argument.</param>
    /// <param name="arg7">The seventh action argument.</param>
    /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
    public static Exception? TryInvoke<T1, T2, T3, T4, T5, T6, T7>(this Action<T1, T2, T3, T4, T5, T6, T7> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4,
        T5 arg5, T6 arg6, T7 arg7)
    {
        var result = default(Exception);
        try
        {
            action(arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        }
        catch (Exception e)
        {
            result = e;
        }

        return result;
    }
    
    /// <summary>
    /// Invokes the action without throwing the exception.
    /// </summary>
    /// <typeparam name="T1">The type of the first action argument.</typeparam>
    /// <typeparam name="T2">The type of the second action argument.</typeparam>
    /// <typeparam name="T3">The type of the third action argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth action argument.</typeparam>
    /// <typeparam name="T5">The type of the fifth action argument.</typeparam>
    /// <typeparam name="T6">The type of the sixth action argument.</typeparam>
    /// <typeparam name="T7">The type of the seventh action argument.</typeparam>
    /// <typeparam name="T8">The type of the eighth action argument.</typeparam>
    /// <param name="action">The action to be invoked.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <param name="arg3">The third action argument.</param>
    /// <param name="arg4">The fourth action argument.</param>
    /// <param name="arg5">The fifth action argument.</param>
    /// <param name="arg6">The sixth action argument.</param>
    /// <param name="arg7">The seventh action argument.</param>
    /// <param name="arg8">The seventh action argument.</param>
    /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
    public static Exception? TryInvoke<T1, T2, T3, T4, T5, T6, T7, T8>(this Action<T1, T2, T3, T4, T5, T6, T7, T8> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4,
        T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        var result = default(Exception);
        try
        {
            action(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }
        catch (Exception e)
        {
            result = e;
        }

        return result;
    }
    
    /// <summary>
    /// Invokes the action without throwing the exception.
    /// </summary>
    /// <typeparam name="T1">The type of the first action argument.</typeparam>
    /// <typeparam name="T2">The type of the second action argument.</typeparam>
    /// <typeparam name="T3">The type of the third action argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth action argument.</typeparam>
    /// <typeparam name="T5">The type of the fifth action argument.</typeparam>
    /// <typeparam name="T6">The type of the sixth action argument.</typeparam>
    /// <typeparam name="T7">The type of the seventh action argument.</typeparam>
    /// <typeparam name="T8">The type of the eighth action argument.</typeparam>
    /// <typeparam name="T9">The type of the ninth action argument.</typeparam>
    /// <param name="action">The action to be invoked.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <param name="arg3">The third action argument.</param>
    /// <param name="arg4">The fourth action argument.</param>
    /// <param name="arg5">The fifth action argument.</param>
    /// <param name="arg6">The sixth action argument.</param>
    /// <param name="arg7">The seventh action argument.</param>
    /// <param name="arg8">The seventh action argument.</param>
    /// <param name="arg9">The ninth action argument.</param>
    /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
    public static Exception? TryInvoke<T1, T2, T3, T4, T5, T6, T7, T8, T9>(this Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4,
        T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        var result = default(Exception);
        try
        {
            action(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }
        catch (Exception e)
        {
            result = e;
        }

        return result;
    }

    /// <summary>
    /// Invokes the action without throwing the exception.
    /// </summary>
    /// <typeparam name="T1">The type of the first action argument.</typeparam>
    /// <typeparam name="T2">The type of the second action argument.</typeparam>
    /// <typeparam name="T3">The type of the third action argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth action argument.</typeparam>
    /// <typeparam name="T5">The type of the fifth action argument.</typeparam>
    /// <typeparam name="T6">The type of the sixth action argument.</typeparam>
    /// <typeparam name="T7">The type of the seventh action argument.</typeparam>
    /// <typeparam name="T8">The type of the eighth action argument.</typeparam>
    /// <typeparam name="T9">The type of the ninth action argument.</typeparam>
    /// <typeparam name="T10">The type of the tenth action argument.</typeparam>
    /// <param name="action">The action to be invoked.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <param name="arg3">The third action argument.</param>
    /// <param name="arg4">The fourth action argument.</param>
    /// <param name="arg5">The fifth action argument.</param>
    /// <param name="arg6">The sixth action argument.</param>
    /// <param name="arg7">The seventh action argument.</param>
    /// <param name="arg8">The seventh action argument.</param>
    /// <param name="arg9">The ninth action argument.</param>
    /// <param name="arg10">The tenth action argument.</param>
    /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
    public static Exception? TryInvoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> action, T1 arg1,
        T2 arg2, T3 arg3, T4 arg4,
        T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        var result = default(Exception);
        try
        {
            action(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        }
        catch (Exception e)
        {
            result = e;
        }

        return result;
    }
}