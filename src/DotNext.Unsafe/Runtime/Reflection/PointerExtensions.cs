using Pointer = System.Reflection.Pointer;

namespace DotNext.Runtime.Reflection;

using Runtime.InteropServices;

/// <summary>
/// Provides interop later between <see cref="Pointer"/> and <see cref="Pointer{T}"/> types.
/// </summary>
public static class PointerExtensions
{
    /// <summary>
    /// Extends
    /// </summary>
    extension(Pointer)
    {
        /// <summary>
        /// Gets the boxed pointer.
        /// </summary>
        /// <param name="pointer">The pointer to be passed to the reflection operations.</param>
        /// <returns>The boxed pointer.</returns>
        [CLSCompliant(false)]
        public static unsafe object Box<T>(Pointer<T> pointer)
            where T : unmanaged
            => Pointer.Box(pointer, typeof(T*));
    }
}