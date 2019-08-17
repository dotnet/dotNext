using System;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using Var = InlineIL.LocalVar;

namespace DotNext.Runtime
{
    /// <summary>
    /// Represents highly optimized runtime intrinsic methods.
    /// </summary>
    public static class Intrinsics
    {
        /// <summary>
        /// Provides the fast way to check whether the specified type accepts  <see langword="null"/> value as valid value.
        /// </summary>
        /// <remarks>
        /// This method always returns <see langword="true"/> for all reference types and <see cref="Nullable{T}"/>.
        /// On mainstream implementations of .NET CLR, this method is replaced by constant value by JIT compiler with zero runtime overhead.
        /// </remarks>
        /// <typeparam name="T">The type to check.</typeparam>
        /// <returns><see langword="true"/> if <typeparamref name="T"/> is nullable type; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullable<T>()
        {
            const string DefaultVar = "default";
            DeclareLocals(true, new Var(DefaultVar, typeof(T)));
            Ldloc(DefaultVar);
            Box(typeof(T));
            Ldnull();
            Ceq();
            return Return<bool>();
        }

        /// <summary>
        /// Returns default value of the given type.
        /// </summary>
        /// <remarks>
        /// This method helps to avoid generation of temporary variables
        /// necessary for <c>default</c> keyword implementation.
        /// </remarks>
        /// <typeparam name="T">The type for which default value should be obtained.</typeparam>
        /// <returns>The default value of type <typeparamref name="T"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T DefaultOf<T>()
        {
            DeclareLocals(true, new Var(typeof(T)));
            Ldloc_0();
            return Return<T>();
        }
    }
}