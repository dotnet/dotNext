using System;
using static System.Runtime.CompilerServices.Unsafe;

namespace DotNext
{
    /// <summary>
    /// Provides extensions for delegate <see cref="Comparison{T}"/>.
    /// </summary>
    public static class Comparison
    {
        /// <summary>
        /// Converts comparison method into <see cref="ValueFunc{T1, T2, R}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the object to compare.</typeparam>
        /// <param name="comparison">The delegate representing comparison method.</param>
        /// <param name="wrap"><see langword="true"/> to wrap <paramref name="comparison"/> into this delegate; <see langword="false"/> to extract method pointer without holding reference to the passed delegate.</param>
        /// <returns>The value delegate represeting comparison method.</returns>
        public static ValueFunc<T, T, int> AsValueFunc<T>(this Comparison<T> comparison, bool wrap = false)
            => new ValueFunc<T, T, int>(As<Func<T, T, int>>(comparison), wrap);
    }
}
