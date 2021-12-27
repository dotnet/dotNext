using System.Runtime.Versioning;

namespace DotNext;

/// <summary>
/// Represents common interface for all option monads.
/// </summary>
/// <typeparam name="T">The type of the value in the container.</typeparam>
public interface IOptionMonad<T> : ISupplier<object?>
{
    /// <summary>
    /// Attempts to get the value in this container.
    /// </summary>
    T Value { get; }

    /// <summary>
    /// Gets the value stored in this container.
    /// </summary>
    /// <returns>The value stored in this container; or <see langword="null"/> if the value is unavaible.</returns>
    object? ISupplier<object?>.Invoke() => Value;

    /// <summary>
    /// Indicates that this monad contains a value.
    /// </summary>
    bool HasValue { get; }

    /// <summary>
    /// If a value is present, returns the value, otherwise return default value.
    /// </summary>
    /// <returns>The value, if present, otherwise default.</returns>
    T? OrDefault();

    /// <summary>
    /// Returns the value if present; otherwise return default value.
    /// </summary>
    /// <param name="defaultValue">The value to be returned if there is no value present.</param>
    /// <returns>The value, if present, otherwise <paramref name="defaultValue"/>.</returns>
    T? Or(T? defaultValue);

    /// <summary>
    /// Returns the value if present; otherwise invoke delegate.
    /// </summary>
    /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
    /// <returns>The value, if present, otherwise returned from delegate.</returns>
    T OrInvoke(Func<T> defaultFunc);

    /// <summary>
    /// Attempts to extract value from container if it is present.
    /// </summary>
    /// <param name="value">Extracted value.</param>
    /// <returns><see langword="true"/> if value is present; otherwise, <see langword="false"/>.</returns>
    bool TryGet(out T? value);
}

/// <summary>
/// Represents common interface for all option monads.
/// </summary>
/// <typeparam name="T">The type of the value in the container.</typeparam>
/// <typeparam name="TSelf">The implementing type.</typeparam>
[RequiresPreviewFeatures]
public interface IOptionMonad<T, TSelf> : IOptionMonad<T>
    where TSelf : struct, IOptionMonad<T, TSelf>
{
    /// <summary>
    /// Places the value to the container.
    /// </summary>
    /// <param name="value">The value to be placed into the container.</param>
    /// <returns>The constructed monad.</returns>
    public static abstract implicit operator TSelf(T value);

    /// <summary>
    /// Attempts to extract the value from the container.
    /// </summary>
    /// <param name="container">The container.</param>
    /// <returns>The extracted value.</returns>
    public static abstract explicit operator T(in TSelf container);

    /// <summary>
    /// Checks whether the container has value.
    /// </summary>
    /// <param name="container">The container to check.</param>
    /// <returns><see langword="true"/> if this container has value; otherwise, <see langword="false"/>.</returns>
    public static abstract bool operator true(in TSelf container);

    /// <summary>
    /// Checks whether the container has no value.
    /// </summary>
    /// <param name="container">The container to check.</param>
    /// <returns><see langword="true"/> if this container has no value; otherwise, <see langword="false"/>.</returns>
    public static abstract bool operator false(in TSelf container);
}