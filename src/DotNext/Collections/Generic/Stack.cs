using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Collections.Generic
{
    /// <summary>
    /// Provides various extension methods for class <see cref="Stack{T}"/>.
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
            if (original.Count == 0)
                return new Stack<T>();
            var arr = original.ToArray();
            Array.Reverse(arr);
            return new Stack<T>(arr);
        }

        /// <summary>
        /// Attempts to obtain an object at the top of the stack without removing it.
        /// </summary>
        /// <param name="stack">Stack instance.</param>
        /// <param name="obj">An object at the top of the stack.</param>
        /// <typeparam name="T">The type of elements in the stack.</typeparam>
        /// <returns><see langword="true"/> if stack is not empty and object at the top of the stack exists; otherwise, <see langword="false"/>.</returns>
        public static bool TryPeek<T>(this Stack<T> stack, [MaybeNull]out T obj)
        {
            if (stack.Count > 0)
            {
                obj = stack.Peek();
                return true;
            }
            else
            {
                obj = default!;
                return false;
            }
        }

        /// <summary>
        /// Attempts to remove object at the top of the stack.
        /// </summary>
        /// <param name="stack">Stack instance.</param>
        /// <param name="obj">An object at the top of the stack.</param>
        /// <typeparam name="T">The type of elements in the stack.</typeparam>
        /// <returns><see langword="true"/> if stack is not empty and object at the top of the stack exists; otherwise, <see langword="false"/>.</returns>
        public static bool TryPop<T>(this Stack<T> stack, [MaybeNull]out T obj)
        {
            if (stack.Count > 0)
            {
                obj = stack.Pop();
                return true;
            }
            else
            {
                obj = default!;
                return false;
            }
        }
    }
}
