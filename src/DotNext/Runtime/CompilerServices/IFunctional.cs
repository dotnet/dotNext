using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices;

/// <summary>
/// Represents root interface for all functional interfaces.
/// </summary>
public interface IFunctional
{
    /// <summary>
    /// Invokes the functional interface dynamically.
    /// </summary>
    /// <param name="args">The location of the first argument.</param>
    /// <param name="count">The number of arguments.</param>
    /// <param name="result">The location of the result. Must be a reference to <see cref="LocalReference{T}"/> value.</param>
    void DynamicInvoke(scoped ref readonly Variant args, int count, scoped Variant result);

    /// <summary>
    /// Gets the argument value.
    /// </summary>
    /// <param name="args">The location of the arguments.</param>
    /// <param name="index">The index of the argument.</param>
    /// <typeparam name="T">The type of the argument.</typeparam>
    /// <returns>The reference to the argument value.</returns>
    protected static ref readonly T GetArgument<T>(ref readonly Variant args, int index)
        where T : allows ref struct
        => ref Unsafe.Add(ref Unsafe.AsRef(in args), index).Immutable<T>();
}