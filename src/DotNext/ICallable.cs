using System;

namespace DotNext
{
    /// <summary>
    /// Represents common interface for typed method pointers.
    /// </summary>
    public interface ICallable
    {
        /// <summary>
        /// Invokes the method dynamically.
        /// </summary>
        /// <param name="args">The arguments to be passed into the method.</param>
        /// <returns>Invocation result.</returns>
        object? DynamicInvoke(Span<object?> args);
    }
}