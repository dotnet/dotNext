using System;
using System.Collections.Generic;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// Provides access to the array allocated
    /// in the unmanaged memory.
    /// </summary>
    /// <typeparam name="T">The type of the array elements.</typeparam>
    public interface IUnmanagedArray<T> : IUnmanagedMemory, IEnumerable<T>
        where T : unmanaged
    {
        /// <summary>
        /// Gets the number of elements in the unmanaged memory.
        /// </summary>
        int Length { get; }

        /// <summary>
        /// Gets a pointer to the allocated unmanaged memory.
        /// </summary>
        new Pointer<T> Pointer { get; }

        /// <summary>
        /// Gets a span from the current instance.
        /// </summary>
        Span<T> Span { get; }
    }
}
