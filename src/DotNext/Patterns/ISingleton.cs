namespace DotNext.Patterns;

/// <summary>
/// Represents singleton pattern.
/// </summary>
/// <typeparam name="TSelf">Singleton type.</typeparam>
public interface ISingleton<out TSelf>
    where TSelf : class, ISingleton<TSelf>
{
    /// <summary>
    /// Gets singleton value.
    /// </summary>
    static abstract TSelf Instance { get; }
}