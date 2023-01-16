using System.Runtime.Versioning;

namespace DotNext;

/// <summary>
/// Represents builder pattern contract.
/// </summary>
/// <typeparam name="TSelf">The type that can be constructed using builder pattern.</typeparam>
/// <typeparam name="TBuilder">The type of the builder.</typeparam>
[RequiresPreviewFeatures]
public interface IBuildable<out TSelf, out TBuilder>
    where TSelf : notnull, IBuildable<TSelf, TBuilder>
    where TBuilder : notnull, ISupplier<TSelf>, IResettable
{
    /// <summary>
    /// Creates a new builder for type <typeparamref name="TSelf"/>.
    /// </summary>
    /// <returns>A new builder for type <typeparamref name="TSelf"/>.</returns>
    public static abstract TBuilder CreateBuilder();
}