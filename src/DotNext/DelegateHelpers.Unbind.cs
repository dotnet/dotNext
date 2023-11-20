namespace DotNext;

public static partial class DelegateHelpers
{
    private static T Unbind<T>(this Delegate del, Type targetType)
        where T : MulticastDelegate
        => del.Target switch
        {
            Closure closure when BasicExtensions.IsContravariant(closure.Target, targetType) => ChangeType<T, EmptyTargetRewriter>(closure.Delegate, default),
            object target when BasicExtensions.IsContravariant(target, targetType) => ChangeType<T, TargetRewriter>(del, default),
            _ => throw new InvalidOperationException(),
        };

    /// <summary>
    /// Converts implicitly bound delegate into its unbound version.
    /// </summary>
    /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
    /// <param name="action">The delegate to unbind.</param>
    /// <returns>Unbound version of the delegate.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravariant to type <typeparamref name="T"/>.</exception>
    public static Action<T> Unbind<T>(this Action action)
        where T : class
        => action.Unbind<Action<T>>(typeof(T));

    /// <summary>
    /// Converts implicitly bound delegate into its unbound version.
    /// </summary>
    /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
    /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
    /// <param name="func">The delegate to unbind.</param>
    /// <returns>Unbound version of the delegate.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravariant to type <typeparamref name="T"/>.</exception>
    public static Func<T, TResult> Unbind<T, TResult>(this Func<TResult> func)
        where T : class
        => func.Unbind<Func<T, TResult>>(typeof(T));

    /// <summary>
    /// Converts implicitly bound delegate into its unbound version.
    /// </summary>
    /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
    /// <typeparam name="TArg">The type of the first explicit parameter.</typeparam>
    /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
    /// <param name="func">The delegate to unbind.</param>
    /// <returns>Unbound version of the delegate.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravariant to type <typeparamref name="T"/>.</exception>
    public static Func<T, TArg, TResult> Unbind<T, TArg, TResult>(this Func<TArg, TResult> func)
        where T : class
        => func.Unbind<Func<T, TArg, TResult>>(typeof(T));

    /// <summary>
    /// Converts implicitly bound delegate into its unbound version.
    /// </summary>
    /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
    /// <typeparam name="TArg">The type of the first explicit parameter.</typeparam>
    /// <param name="action">The delegate to unbind.</param>
    /// <returns>Unbound version of the delegate.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravariant to type <typeparamref name="T"/>.</exception>
    public static Action<T, TArg> Unbind<T, TArg>(this Action<TArg> action)
        where T : class
        => action.Unbind<Action<T, TArg>>(typeof(T));

    /// <summary>
    /// Converts implicitly bound delegate into its unbound version.
    /// </summary>
    /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
    /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
    /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
    /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
    /// <param name="func">The delegate to unbind.</param>
    /// <returns>Unbound version of the delegate.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravariant to type <typeparamref name="T"/>.</exception>
    public static Func<T, T1, T2, TResult> Unbind<T, T1, T2, TResult>(this Func<T1, T2, TResult> func)
        where T : class
        => func.Unbind<Func<T, T1, T2, TResult>>(typeof(T));

    /// <summary>
    /// Converts implicitly bound delegate into its unbound version.
    /// </summary>
    /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
    /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
    /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
    /// <param name="action">The delegate to unbind.</param>
    /// <returns>Unbound version of the delegate.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravariant to type <typeparamref name="T"/>.</exception>
    public static Action<T, T1, T2> Unbind<T, T1, T2>(this Action<T1, T2> action)
        where T : class
        => action.Unbind<Action<T, T1, T2>>(typeof(T));

    /// <summary>
    /// Converts implicitly bound delegate into its unbound version.
    /// </summary>
    /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
    /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
    /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
    /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
    /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
    /// <param name="func">The delegate to unbind.</param>
    /// <returns>Unbound version of the delegate.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravariant to type <typeparamref name="T"/>.</exception>
    public static Func<T, T1, T2, T3, TResult> Unbind<T, T1, T2, T3, TResult>(this Func<T1, T2, T3, TResult> func)
        where T : class
        => func.Unbind<Func<T, T1, T2, T3, TResult>>(typeof(T));

    /// <summary>
    /// Converts implicitly bound delegate into its unbound version.
    /// </summary>
    /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
    /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
    /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
    /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
    /// <param name="action">The delegate to unbind.</param>
    /// <returns>Unbound version of the delegate.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravariant to type <typeparamref name="T"/>.</exception>
    public static Action<T, T1, T2, T3> Unbind<T, T1, T2, T3>(this Action<T1, T2, T3> action)
        where T : class
        => action.Unbind<Action<T, T1, T2, T3>>(typeof(T));

    /// <summary>
    /// Converts implicitly bound delegate into its unbound version.
    /// </summary>
    /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
    /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
    /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
    /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth explicit parameter.</typeparam>
    /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
    /// <param name="func">The delegate to unbind.</param>
    /// <returns>Unbound version of the delegate.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravariant to type <typeparamref name="T"/>.</exception>
    public static Func<T, T1, T2, T3, T4, TResult> Unbind<T, T1, T2, T3, T4, TResult>(this Func<T1, T2, T3, T4, TResult> func)
        where T : class
        => func.Unbind<Func<T, T1, T2, T3, T4, TResult>>(typeof(T));

    /// <summary>
    /// Converts implicitly bound delegate into its unbound version.
    /// </summary>
    /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
    /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
    /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
    /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth explicit parameter.</typeparam>
    /// <param name="action">The delegate to unbind.</param>
    /// <returns>Unbound version of the delegate.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravariant to type <typeparamref name="T"/>.</exception>
    public static Action<T, T1, T2, T3, T4> Unbind<T, T1, T2, T3, T4>(this Action<T1, T2, T3, T4> action)
        where T : class
        => action.Unbind<Action<T, T1, T2, T3, T4>>(typeof(T));

    /// <summary>
    /// Converts implicitly bound delegate into its unbound version.
    /// </summary>
    /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
    /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
    /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
    /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth explicit parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth explicit parameter.</typeparam>
    /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
    /// <param name="func">The delegate to unbind.</param>
    /// <returns>Unbound version of the delegate.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravariant to type <typeparamref name="T"/>.</exception>
    public static Func<T, T1, T2, T3, T4, T5, TResult> Unbind<T, T1, T2, T3, T4, T5, TResult>(this Func<T1, T2, T3, T4, T5, TResult> func)
        where T : class
        => func.Unbind<Func<T, T1, T2, T3, T4, T5, TResult>>(typeof(T));

    /// <summary>
    /// Converts implicitly bound delegate into its unbound version.
    /// </summary>
    /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
    /// <typeparam name="T1">The type of the first explicit parameter.</typeparam>
    /// <typeparam name="T2">The type of the second explicit parameter.</typeparam>
    /// <typeparam name="T3">The type of the third explicit parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth explicit parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth explicit parameter.</typeparam>
    /// <param name="action">The delegate to unbind.</param>
    /// <returns>Unbound version of the delegate.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="action"/> is not contravariant to type <typeparamref name="T"/>.</exception>
    public static Action<T, T1, T2, T3, T4, T5> Unbind<T, T1, T2, T3, T4, T5>(this Action<T1, T2, T3, T4, T5> action)
        where T : class
        => action.Unbind<Action<T, T1, T2, T3, T4, T5>>(typeof(T));

    /// <summary>
    /// Converts implicitly bound delegate into its unbound version.
    /// </summary>
    /// <typeparam name="T">The expected type of <see cref="Delegate.Target"/>.</typeparam>
    /// <param name="func">The delegate to unbind.</param>
    /// <returns>Unbound version of the delegate.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Delegate.Target"/> of <paramref name="func"/> is not contravariant to type <typeparamref name="T"/>.</exception>
    public static Predicate<T> Unbind<T>(this Func<bool> func)
        where T : class
        => func.Unbind<Predicate<T>>(typeof(T));
}