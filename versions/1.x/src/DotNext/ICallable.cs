using System;

namespace DotNext
{
    /// <summary>
    /// Represents common interface for typed method pointers.
    /// </summary>
    public interface ICallable
    {
        /// <summary>
        /// Gets object targeted by this pointer.
        /// </summary>
        /// <value>The object targeted by this pointer.</value>
        object Target { get; }

        /// <summary>
        /// Invokes the method dynamically.
        /// </summary>
        /// <param name="args">The arguments to be passed into the method.</param>
        /// <returns>Invocation result.</returns>
        object DynamicInvoke(params object[] args);

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        bool IsEmpty { get; }
    }

    /// <summary>
    /// Represents common interface for typed method pointers.
    /// </summary>
    /// <typeparam name="D">The type of the delegate that is compatible with the pointer.</typeparam>
    public interface ICallable<out D> : ICallable
        where D : Delegate
    {
        /// <summary>
        /// Converts method pointer into delegate.
        /// </summary>
        /// <returns>The delegate instance created from this pointer.</returns>
        D ToDelegate();
    }
}