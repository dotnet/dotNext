namespace DotNext;

using Patterns;

/// <summary>
/// Represents range endpoint.
/// </summary>
/// <typeparam name="T">The type of the endpoint.</typeparam>
public interface IRangeEndpoint<in T>
    where T : notnull
{
    /// <summary>
    /// Checks whether the specified value is on the left side from this endpoint.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is on the left side from this endpoint.</returns>
    bool IsOnLeft(T value);

    /// <summary>
    /// Checks whether the specified value is on the right side from this endpoint.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is on the right side from this endpoint.</returns>
    bool IsOnRight(T value);

    /// <summary>
    /// Gets infinite endpoint.
    /// </summary>
    public static IRangeEndpoint<T> Infinity => InfinityEndpoint<T>.Instance;
}

/// <summary>
/// Represents finite range endpoint.
/// </summary>
/// <typeparam name="T">The type of the endpoint.</typeparam>
public interface IFiniteRangeEndpoint<T> : IRangeEndpoint<T>
    where T : notnull
{
    /// <summary>
    /// Gets the value of the endpoint.
    /// </summary>
    T Value { get; }
}

/// <summary>
/// Represents enclosed range endpoint.
/// </summary>
/// <typeparam name="T">The type of the endpoint.</typeparam>
public readonly struct EnclosedEndpoint<T> : IFiniteRangeEndpoint<T>
    where T : IComparable<T>
{
    /// <summary>
    /// Gets a value of this endpoint.
    /// </summary>
    public required T Value
    {
        get;
        init;
    }

    /// <inheritdoc/>
    bool IRangeEndpoint<T>.IsOnLeft(T value) => value.CompareTo(Value) <= 0;

    /// <inheritdoc/>
    bool IRangeEndpoint<T>.IsOnRight(T value) => value.CompareTo(Value) >= 0;

    /// <summary>
    /// Converts enclosed endpoint to disclosed endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint to convert.</param>
    /// <returns>The disclosed endpoint.</returns>
    public static DisclosedEndpoint<T> operator ~(EnclosedEndpoint<T> endpoint)
        => new() { Value = endpoint.Value };

    /// <summary>
    /// Creates a range endpoint that is included to the range.
    /// </summary>
    /// <param name="value">The value of the endpoint.</param>
    public static implicit operator EnclosedEndpoint<T>(T value) => new() { Value = value };
}

/// <summary>
/// Represents disclosed range endpoint.
/// </summary>
/// <typeparam name="T">The type of the endpoint.</typeparam>
public readonly struct DisclosedEndpoint<T> : IFiniteRangeEndpoint<T>
    where T : IComparable<T>
{
    /// <summary>
    /// Gets a value of this endpoint.
    /// </summary>
    public required T Value
    {
        get;
        init;
    }

    /// <inheritdoc/>
    bool IRangeEndpoint<T>.IsOnLeft(T value) => value.CompareTo(Value) < 0;

    /// <inheritdoc/>
    bool IRangeEndpoint<T>.IsOnRight(T value) => value.CompareTo(Value) > 0;

    /// <summary>
    /// Converts disclosed endpoint to enclosed endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint to convert.</param>
    /// <returns>The enclosed endpoint.</returns>
    public static EnclosedEndpoint<T> operator ~(DisclosedEndpoint<T> endpoint)
        => new() { Value = endpoint.Value };

    /// <summary>
    /// Creates a range endpoint that is excluded from the range.
    /// </summary>
    /// <param name="value">The value of the endpoint.</param>
    public static implicit operator DisclosedEndpoint<T>(T value) => new() { Value = value };
}

file sealed class InfinityEndpoint<T> : IRangeEndpoint<T>, ISingleton<InfinityEndpoint<T>>
    where T : notnull
{
    public static InfinityEndpoint<T> Instance { get; } = new();

    private InfinityEndpoint()
    {
    }

    bool IRangeEndpoint<T>.IsOnLeft(T value) => true;

    bool IRangeEndpoint<T>.IsOnRight(T value) => true;
}