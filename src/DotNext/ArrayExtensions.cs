using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;

namespace DotNext;

/// <summary>
/// Providers extensions for <see cref="Array"/> type.
/// </summary>
public static class ArrayExtensions
{
    /// <summary>
    /// Extends <see cref="Array"/> type.
    /// </summary>
    /// <param name="a">The array to extend.</param>
    extension(Array a)
    {
        /// <summary>
        /// Gets length of the array.
        /// </summary>
        /// <remarks>
        /// This method supports one-dimensional as well as multidimensional arrays.
        /// </remarks>
        /// <param name="array">The array object.</param>
        /// <returns>The length of the array as native unsigned integer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static nuint GetLength(Array array)
        {
            Push(array);
            Ldlen();
            return Return<nuint>();
        }

        /// <summary>
        /// Indicates that the array is empty.
        /// </summary>
        public bool IsEmpty => GetLength(a) is 0;

        /// <summary>
        /// Indicates that array is <see langword="null"/> or empty.
        /// </summary>
        /// <param name="array">The array to check.</param>
        /// <returns><see langword="true"/>, if array is <see langword="null"/> or empty.</returns>
        public static bool IsNullOrEmpty([NotNullWhen(false)] Array? array)
            => array is null or { IsEmpty: true };
    }
}