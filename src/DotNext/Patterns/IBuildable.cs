namespace DotNext.Patterns;

/// <summary>
/// Represents builder pattern contract.
/// </summary>
/// <typeparam name="TSelf">The type that can be constructed using builder pattern.</typeparam>
/// <typeparam name="TBuilder">The type of the builder.</typeparam>
public interface IBuildable<out TSelf, out TBuilder>
    where TSelf : IBuildable<TSelf, TBuilder>
    where TBuilder : ISupplier<TSelf>, IResettable
{
    /// <summary>
    /// Creates a new builder for type <typeparamref name="TSelf"/>.
    /// </summary>
    /// <returns>A new builder for type <typeparamref name="TSelf"/>.</returns>
    public static abstract TBuilder CreateBuilder();
}