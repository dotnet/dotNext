namespace DotNext;

/// <summary>
/// Represents the common interface for Result monad.
/// </summary>
/// <typeparam name="TError">The type that represents an error.</typeparam>
public interface IResultMonad<out TError>
    where TError : notnull
{
    /// <summary>
    /// Gets the error.
    /// </summary>
    TError? Error { get; }
}

/// <summary>
/// Represents the common interface for Result monad.
/// </summary>
/// <typeparam name="T">The type of the result.</typeparam>
/// <typeparam name="TError">The type that represents an error.</typeparam>
public interface IResultMonad<T, out TError> : IResultMonad<TError>, IOptionMonad<T>
    where TError : notnull
{
    /// <summary>
    /// Returns the value if present; otherwise invoke delegate.
    /// </summary>
    /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
    /// <returns>The value, if present, otherwise returned from delegate.</returns>
    T OrInvoke(Func<TError, T> defaultFunc) => HasValue ? ValueOrDefault! : defaultFunc(Error!);
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

    /// <summary>
    /// Converts the result to <see cref="Optional{T}"/> monad.
    /// </summary>
    /// <param name="result">The result to be converted.</param>
    /// <returns>The converted result.</returns>
    public static virtual implicit operator Optional<T>(in TSelf result)
        => result.HasValue ? result.ValueOrDefault : Optional<T>.None;
}