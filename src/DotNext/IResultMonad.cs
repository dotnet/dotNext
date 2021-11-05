using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;

namespace DotNext;

/// <summary>
/// Represents common interface for Result monad.
/// </summary>
/// <typeparam name="T">The type of the result.</typeparam>
/// <typeparam name="TError">The type that represents an error.</typeparam>
public interface IResultMonad<T, out TError> : IOptionMonad<T>
{
    /// <summary>
    /// Gets the error.
    /// </summary>
    TError Error { get; }
}

/// <summary>
/// Represents common interface for Result monad.
/// </summary>
/// <typeparam name="T">The type of the result.</typeparam>
/// <typeparam name="TError">The type that represents an error.</typeparam>
/// <typeparam name="TSelf">The implementing type.</typeparam>
[RequiresPreviewFeatures]
public interface IResultMonad<T, TError, TSelf> : IResultMonad<T, TError>, IOptionMonad<T, TSelf>
    where TSelf : struct, IResultMonad<T, TError, TSelf>
{
    /// <summary>
    /// Creates unsuccessful result.
    /// </summary>
    /// <param name="error">The error representing unsuccessful result.</param>
    /// <returns>The unsuccessful result.</returns>
    public static abstract TSelf FromError([DisallowNull] TError error);

    /// <summary>
    /// Converts the result to <see cref="Optional{T}"/> monad.
    /// </summary>
    /// <param name="result">The result to be converted.</param>
    /// <returns>The converted result.</returns>
    public static abstract implicit operator Optional<T>(in TSelf result);
}