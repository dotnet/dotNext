namespace DotNext;

/// <summary>
/// Represents common contract for all mutable objects that support revert of their internal state.
/// </summary>
public interface IResettable
{
    /// <summary>
    /// Resets the internal state.
    /// </summary>
    void Reset();
}