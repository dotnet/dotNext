namespace DotNext;

/// <summary>
/// Represents common interface for all option monads.
/// </summary>
/// <typeparam name="T">The type of the value in the container.</typeparam>
public interface IOptionMonad<T> : ISupplier<object?>
{
    /// <summary>
    /// Gets the value stored in this container.
    /// </summary>
    /// <returns>The value stored in this container; or <see langword="null"/> if the value is unavailable.</returns>
    object? ISupplier<object?>.Invoke() => TryGet(out var result) ? result : null;

    /// <summary>
    /// Indicates that this monad contains a value.
    /// </summary>
    bool HasValue { get; }

    /// <summary>
    /// If a value is present, returns the value, otherwise return default value.
    /// </summary>
    /// <value>The value, if present, otherwise default.</value>
    T? ValueOrDefault { get; }

    /// <summary>
    /// Returns the value if present; otherwise return default value.
    /// </summary>
    /// <param name="defaultValue">The value to be returned if there is no value present.</param>
    /// <returns>The value, if present, otherwise <paramref name="defaultValue"/>.</returns>
    T? Or(T? defaultValue) => HasValue ? ValueOrDefault : defaultValue;

    /// <summary>
    /// Returns the value if present; otherwise invoke delegate.
    /// </summary>
    /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
    /// <returns>The value, if present, otherwise returned from delegate.</returns>
    T OrInvoke(Func<T> defaultFunc) => HasValue ? ValueOrDefault! : defaultFunc();

    /// <summary>
    /// Attempts to extract value from container if it is present.
    /// </summary>
    /// <param name="value">Extracted value.</param>
    /// <returns><see langword="true"/> if value is present; otherwise, <see langword="false"/>.</returns>
    bool TryGet(out T? value)
    {
        value = ValueOrDefault;
        return HasValue;
    }
}

/// <summary>
/// Represents common interface for all option monads.
/// </summary>
/// <typeparam name="T">The type of the value in the container.</typeparam>
/// <typeparam name="TSelf">The implementing type.</typeparam>
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
    public static virtual bool operator true(in TSelf container)
        => container.HasValue;

    /// <summary>
    /// Checks whether the container has no value.
    /// </summary>
    /// <param name="container">The container to check.</param>
    /// <returns><see langword="true"/> if this container has no value; otherwise, <see langword="false"/>.</returns>
    public static virtual bool operator false(in TSelf container) => !container;

    /// <summary>
    /// Checks whether the container has no value.
    /// </summary>
    /// <param name="container">The container to check.</param>
    /// <returns><see langword="true"/> if this container has no value; otherwise, <see langword="false"/>.</returns>
    public static virtual bool operator !(in TSelf container) => container.HasValue is false;

    /// <summary>
    /// Returns the value if present; otherwise return default value.
    /// </summary>
    /// <param name="container">The container to check.</param>
    /// <param name="defaultValue">The value to be returned if there is no value present.</param>
    /// <returns>The value, if present, otherwise <paramref name="defaultValue"/>.</returns>
    public static virtual T? operator |(in TSelf container, T? defaultValue)
        => container.HasValue ? container.ValueOrDefault : defaultValue;

    /// <summary>
    /// Returns non-empty container.
    /// </summary>
    /// <param name="x">The first container.</param>
    /// <param name="y">The second container.</param>
    /// <returns>The first non-empty container.</returns>
    public static virtual TSelf operator |(in TSelf x, in TSelf y)
        => x.HasValue ? x : y;
}