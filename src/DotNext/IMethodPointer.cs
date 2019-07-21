using System;

namespace DotNext
{
    /// <summary>
    /// Represents common interface for typed method pointers.
    /// </summary>
    /// <typeparam name="D">The type of the delegate that is compatible with the pointer.</typeparam>
    public interface IMethodPointer<out D>
        where D : Delegate
    {
        /// <summary>
        /// Converts method pointer into delegate.
        /// </summary>
        /// <returns>The delegate instance created from this pointer.</returns>
        D ToDelegate();

        /// <summary>
        /// Gets address of the method.
        /// </summary>
        IntPtr Address { get; }

        /// <summary>
        /// Gets object targeted by this pointer.
        /// </summary>
        /// <value>The object targeted by this pointer.</value>
        object Target { get; }
    }
}