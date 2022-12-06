using System.Diagnostics.CodeAnalysis;

namespace DotNext.Generic;

/// <summary>
/// Allows to use constant values as generic parameters.
/// </summary>
/// <remarks>
/// Derived class must be sealed or abstract. If class is sealed
/// then it should have at least one constructor without parameters.
/// </remarks>
/// <typeparam name="T">Type of constant to be passed as generic parameter.</typeparam>
public abstract class Constant<T> : ISupplier<T>
{
    /// <summary>
    /// Initializes a new generic-level constant.
    /// </summary>
    /// <param name="constVal">Constant value.</param>
    protected Constant(T constVal) => Value = constVal;

    /// <summary>
    /// Gets value of the constant.
    /// </summary>
    public T Value { get; }

    /// <inheritdoc/>
    T ISupplier<T>.Invoke() => Value;

    /// <summary>
    /// Returns textual representation of the constant value.
    /// </summary>
    /// <returns>The textual representation of the constant value.</returns>
    public sealed override string ToString() => Value?.ToString() ?? "NULL";

    /// <summary>
    /// Computes hash code for the constant.
    /// </summary>
    /// <returns>The hash code of the constant.</returns>
    public sealed override int GetHashCode() => Value?.GetHashCode() ?? 0;

    /// <summary>
    /// Determines whether two constant values are equal.
    /// </summary>
    /// <param name="other">Other constant value to compare.</param>
    /// <returns><see langword="true"/>, this object represents the same constant value as other; otherwise, <see langword="false"/>.</returns>
    public sealed override bool Equals([NotNullWhen(true)] object? other) => other switch
    {
        T obj => Equals(obj, Value),
        Constant<T> @const => Equals(Value, @const.Value),
        _ => false,
    };

    /// <summary>
    /// Extracts constant value.
    /// </summary>
    /// <param name="const">The constant value holder.</param>
    [return: NotNullIfNotNull(nameof(@const))]
    public static implicit operator T?(Constant<T>? @const) => @const is null ? default : @const.Value;
}