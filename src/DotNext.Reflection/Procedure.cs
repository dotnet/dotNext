using System;
using System.Diagnostics.CodeAnalysis;

namespace DotNext
{
    /// <summary>
    /// Represents a static procedure with arbitrary number of arguments
    /// allocated on the stack.
    /// </summary>
    /// <param name="arguments">Procedure arguments in the form of public structure fields.</param>
    /// <typeparam name="TArgs">Type of structure with procedure arguments allocated on the stack.</typeparam>
    public delegate void Procedure<TArgs>(in TArgs arguments)
        where TArgs : struct;

    /// <summary>
    /// Represents an instance procedure with arbitrary number of arguments
    /// allocated on the stack.
    /// </summary>
    /// <param name="this">Hidden <c>this</c> parameter.</param>
    /// <param name="arguments">Procedure arguments in the form of public structure fields.</param>
    /// <typeparam name="T">Type of instance to be passed into underlying method.</typeparam>
    /// <typeparam name="TArgs">Type of structure with procedure arguments allocated on the stack.</typeparam>
    public delegate void Procedure<T, TArgs>([DisallowNull]in T @this, in TArgs arguments)
        where TArgs : struct;

    /// <summary>
    /// Provides extension methods for delegates <see cref="Procedure{A}"/> and <see cref="Procedure{T, A}"/>.
    /// </summary>
    public static class Procedure
    {
        private sealed class Closure<T, TArgs>
            where TArgs : struct
        {
            private readonly Procedure<T, TArgs> procedure;
            [NotNull]
            private readonly T target;

            internal Closure(Procedure<T, TArgs> procedure, [DisallowNull]T target)
            {
                this.procedure = procedure;
                this.target = target;
            }

            internal void Invoke(in TArgs arguments) => procedure(target, arguments);
        }

        /// <summary>
        /// Converts <see cref="Procedure{T, A}"/> into <see cref="Procedure{A}"/> through
        /// capturing of the first argument of <see cref="Procedure{T, A}"/> delegate.
        /// </summary>
        /// <typeparam name="T">Type of instance to be passed into underlying method.</typeparam>
        /// <typeparam name="TArgs">Type of structure with procedure arguments allocated on the stack.</typeparam>
        /// <param name="procedure">The procedure to be converted.</param>
        /// <param name="this">The first argument to be captured.</param>
        /// <returns>The procedure instance.</returns>
        public static Procedure<TArgs> Bind<T, TArgs>(this Procedure<T, TArgs> procedure, [DisallowNull]T @this)
            where TArgs : struct
            => new Closure<T, TArgs>(procedure, @this).Invoke;

        /// <summary>
        /// Converts <see cref="Procedure{T, A}"/> into <see cref="Procedure{A}"/> through
        /// capturing of the first argument of <see cref="Procedure{T, A}"/> delegate.
        /// </summary>
        /// <typeparam name="T">Type of instance to be passed into underlying method.</typeparam>
        /// <typeparam name="TArgs">Type of structure with procedure arguments allocated on the stack.</typeparam>
        /// <param name="procedure">The procedure to be converted.</param>
        /// <param name="this">The first argument to be captured.</param>
        /// <returns>The procedure instance.</returns>
        [Obsolete("Use Bind method instead", true)]
        public static Procedure<TArgs> Capture<T, TArgs>(Procedure<T, TArgs> procedure, [DisallowNull]T @this)
            where TArgs : struct
            => Bind(procedure, @this);

        /// <summary>
        /// Allocates list of arguments on the stack.
        /// </summary>
        /// <typeparam name="TArgs">The type representing list of arguments.</typeparam>
        /// <param name="procedure">The procedure instance.</param>
        /// <returns>Allocated list of arguments.</returns>
        public static TArgs ArgList<TArgs>(this Procedure<TArgs> procedure)
            where TArgs : struct
            => new TArgs();

        /// <summary>
        /// Allocates list of arguments on the stack.
        /// </summary>
        /// <typeparam name="T">Type of explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="TArgs">The type representing list of arguments.</typeparam>
        /// <param name="procedure">The procedure instance.</param>
        /// <returns>Allocated list of arguments.</returns>
        public static TArgs ArgList<T, TArgs>(this Procedure<T, TArgs> procedure)
            where TArgs : struct
            => new TArgs();

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        public static void Invoke<T>(this Procedure<T, ValueTuple> procedure, [DisallowNull]in T instance)
            => procedure(in instance, default);

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <param name="procedure">The function to be invoked.</param>
        public static void Invoke(this Procedure<ValueTuple> procedure)
            => procedure(default);

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="TParam">The type of the first function argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg">The first procedure argument.</param>
        public static void Invoke<T, TParam>(this Procedure<T, ValueTuple<TParam>> procedure, [DisallowNull]in T instance, TParam arg)
            => procedure(in instance, new ValueTuple<TParam>(arg));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the first procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg">The first procedure argument.</param>
        public static void Invoke<T>(this Procedure<ValueTuple<T>> procedure, T arg)
            => procedure(new ValueTuple<T>(arg));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        public static void Invoke<T, T1, T2>(this Procedure<T, (T1, T2)> procedure, [DisallowNull]in T instance, T1 arg1, T2 arg2)
            => procedure(in instance, (arg1, arg2));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        public static void Invoke<T1, T2>(this Procedure<(T1, T2)> procedure, T1 arg1, T2 arg2)
            => procedure((arg1, arg2));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        public static void Invoke<T, T1, T2, T3>(this Procedure<T, (T1, T2, T3)> procedure, [DisallowNull]in T instance, T1 arg1, T2 arg2, T3 arg3)
            => procedure(in instance, (arg1, arg2, arg3));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        public static void Invoke<T1, T2, T3>(this Procedure<(T1, T2, T3)> procedure, T1 arg1, T2 arg2, T3 arg3)
            => procedure((arg1, arg2, arg3));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        public static void Invoke<T, T1, T2, T3, T4>(this Procedure<T, (T1, T2, T3, T4)> procedure, [DisallowNull]in T instance, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => procedure(in instance, (arg1, arg2, arg3, arg4));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        public static void Invoke<T1, T2, T3, T4>(this Procedure<(T1, T2, T3, T4)> procedure, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => procedure((arg1, arg2, arg3, arg4));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        public static void Invoke<T, T1, T2, T3, T4, T5>(this Procedure<T, (T1, T2, T3, T4, T5)> procedure, [DisallowNull]in T instance, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => procedure(in instance, (arg1, arg2, arg3, arg4, arg5));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        public static void Invoke<T1, T2, T3, T4, T5>(this Procedure<(T1, T2, T3, T4, T5)> procedure, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => procedure((arg1, arg2, arg3, arg4, arg5));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        public static void Invoke<T, T1, T2, T3, T4, T5, T6>(this Procedure<T, (T1, T2, T3, T4, T5, T6)> procedure, [DisallowNull]in T instance, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            => procedure(in instance, (arg1, arg2, arg3, arg4, arg5, arg6));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        public static void Invoke<T1, T2, T3, T4, T5, T6>(this Procedure<(T1, T2, T3, T4, T5, T6)> procedure, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            => procedure((arg1, arg2, arg3, arg4, arg5, arg6));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        /// <param name="arg7">The seventh procedure argument.</param>
        public static void Invoke<T, T1, T2, T3, T4, T5, T6, T7>(this Procedure<T, (T1, T2, T3, T4, T5, T6, T7)> procedure, [DisallowNull]in T instance, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
            => procedure(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        /// <param name="arg7">The seventh procedure argument.</param>
        public static void Invoke<T1, T2, T3, T4, T5, T6, T7>(this Procedure<(T1, T2, T3, T4, T5, T6, T7)> procedure, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
            => procedure((arg1, arg2, arg3, arg4, arg5, arg6, arg7));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh procedure argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        /// <param name="arg7">The seventh procedure argument.</param>
        /// <param name="arg8">The eighth procedure argument.</param>
        public static void Invoke<T, T1, T2, T3, T4, T5, T6, T7, T8>(this Procedure<T, (T1, T2, T3, T4, T5, T6, T7, T8)> procedure, [DisallowNull]in T instance, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
            => procedure(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh procedure argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        /// <param name="arg7">The seventh procedure argument.</param>
        /// <param name="arg8">The eighth procedure argument.</param>
        public static void Invoke<T1, T2, T3, T4, T5, T6, T7, T8>(this Procedure<(T1, T2, T3, T4, T5, T6, T7, T8)> procedure, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
            => procedure((arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh procedure argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth procedure argument.</typeparam>
        /// <typeparam name="T9">The type of the ninth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        /// <param name="arg7">The seventh procedure argument.</param>
        /// <param name="arg8">The eighth procedure argument.</param>
        /// <param name="arg9">The ninth procedure argument.</param>
        public static void Invoke<T, T1, T2, T3, T4, T5, T6, T7, T8, T9>(this Procedure<T, (T1, T2, T3, T4, T5, T6, T7, T8, T9)> procedure, [DisallowNull]in T instance, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
            => procedure(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh procedure argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth procedure argument.</typeparam>
        /// <typeparam name="T9">The type of the ninth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        /// <param name="arg7">The seventh procedure argument.</param>
        /// <param name="arg8">The eighth procedure argument.</param>
        /// <param name="arg9">The ninth procedure argument.</param>
        public static void Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9>(this Procedure<(T1, T2, T3, T4, T5, T6, T7, T8, T9)> procedure, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
            => procedure((arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh procedure argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth procedure argument.</typeparam>
        /// <typeparam name="T9">The type of the ninth procedure argument.</typeparam>
        /// <typeparam name="T10">The type of the tenth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        /// <param name="arg7">The seventh procedure argument.</param>
        /// <param name="arg8">The eighth procedure argument.</param>
        /// <param name="arg9">The ninth procedure argument.</param>
        /// <param name="arg10">The tenth procedure argument.</param>
        public static void Invoke<T, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this Procedure<T, (T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)> procedure, [DisallowNull]in T instance, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
            => procedure(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh procedure argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth procedure argument.</typeparam>
        /// <typeparam name="T9">The type of the ninth procedure argument.</typeparam>
        /// <typeparam name="T10">The type of the tenth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        /// <param name="arg7">The seventh procedure argument.</param>
        /// <param name="arg8">The eighth procedure argument.</param>
        /// <param name="arg9">The ninth procedure argument.</param>
        /// <param name="arg10">The tenth procedure argument.</param>
        public static void Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this Procedure<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)> procedure, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
            => procedure((arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));
    }
}
