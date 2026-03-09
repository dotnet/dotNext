namespace DotNext;

/// <summary>
/// Represents the common interface for Result monad.
/// </summary>
/// <typeparam name="T">The type of the result.</typeparam>
/// <typeparam name="TError">The type that represents an error.</typeparam>
public interface IResultMonad<T, out TError> : IOptionMonad<T>
    where TError : notnull
{
    /// <summary>
    /// Gets the error.
    /// </summary>
    TError? Error { get; }

    /// <summary>
    /// Returns the value if present; otherwise invoke delegate.
    /// </summary>
    /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
    /// <returns>The value, if present, otherwise returned from delegate.</returns>
    T OrInvoke(Func<TError, T> defaultFunc) => HasValue ? ValueOrDefault! : defaultFunc(Error!);
}

/// <summary>
/// Represents Result monad where error is represented by the exception.
/// </summary>
/// <typeparam name="T">The type of the result.</typeparam>
public interface IResultMonad<T> : IResultMonad<T, Exception>
{
    /// <summary>
    /// Gets the value of the monad.
    /// </summary>
    /// <exception cref="Exception">The underlying exception is thrown.</exception>
    T Value { get; }
}

/// <summary>
/// Represents the common interface for Result monad.
/// </summary>
/// <typeparam name="T">The type of the result.</typeparam>
/// <typeparam name="TError">The type that represents an error.</typeparam>
/// <typeparam name="TSelf">The implementing type.</typeparam>
public interface IResultMonad<T, TError, TSelf> : IResultMonad<T, TError>, IOptionMonad<T, TSelf>
    where TError : notnull
    where TSelf : struct, IResultMonad<T, TError, TSelf>
{
    /// <summary>
    /// Creates unsuccessful result.
    /// </summary>
    /// <param name="error">The error representing unsuccessful result.</param>
    /// <returns>The unsuccessful result.</returns>
    public static abstract TSelf FromError(TError error);
}