using System;
using System.Collections.Generic;

namespace DotNext.Collections.Generic
{
    /// <summary>
    /// Provides various extension method for class <see cref="Stack{T}"/>.
    /// </summary>
    public static class Stack
    {
        /// <summary>
        /// Creates a clone of the stack preserving order of elements into it.
        /// </summary>
        /// <typeparam name="T">Type of elements in the stack.</typeparam>
        /// <param name="original">The stack to clone.</param>
        /// <returns>A cloned stack.</returns>
        public static Stack<T> Clone<T>(this Stack<T> original)
        {
            var arr = original.ToArray();
            Array.Reverse(arr);
            return new Stack<T>(arr);
        }
    }
}
