using System;

namespace DotNext
{
    /// <summary>
    /// Represents a static procedure with arbitrary number of arguments
    /// allocated on the stack.
    /// </summary>
    /// <param name="arguments">Procedure arguments in the form of public structure fields.</param>
    /// <typeparam name="A">Type of structure with procedure arguments allocated on the stack.</typeparam>
    public delegate void Procedure<A>(in A arguments) where A : struct;

    /// <summary>
    /// Represents an instance procedure with arbitrary number of arguments
    /// allocated on the stack.
    /// </summary>
    /// <param name="this">Hidden <c>this</c> parameter.</param>
    /// <param name="arguments">Procedure arguments in the form of public structure fields.</param>
    /// <typeparam name="T">Type of instance to be passed into underlying method.</typeparam>
    /// <typeparam name="A">Type of structure with procedure arguments allocated on the stack.</typeparam>
    public delegate void Procedure<T, A>(in T @this, in A arguments) where A : struct;

    /// <summary>
    /// Provides extension methods for delegates <see cref="Procedure{A}"/> and <see cref="Procedure{T, A}"/>.
    /// </summary>
	public static class Procedure
    {
        private sealed class Closure<T, A>
            where A : struct
        {
            private readonly Procedure<T, A> procedure;
            private readonly T target;

            internal Closure(Procedure<T, A> procedure, T target)
            {
                this.procedure = procedure;
                this.target = target;
            }

            internal void Invoke(in A arguments) => procedure(target, arguments);
        }

        /// <summary>
        /// Converts <see cref="Procedure{T, A}"/> into <see cref="Procedure{A}"/> through
        /// capturing of the first argument of <see cref="Procedure{T, A}"/> delegate.
        /// </summary>
        /// <typeparam name="T">Type of instance to be passed into underlying method.</typeparam>
        /// <typeparam name="A">Type of structure with procedure arguments allocated on the stack.</typeparam>
        /// <param name="procedure">The procedure to be converted.</param>
        /// <param name="this">The first argument to be captured.</param>
        /// <returns>The procedure instance.</returns>
        public static Procedure<A> Capture<T, A>(this Procedure<T, A> procedure, T @this) where A : struct => new Closure<T, A>(procedure, @this).Invoke;

        /// <summary>
        /// Allocates list of arguments on the stack.
        /// </summary>
        /// <typeparam name="A">The type representing list of arguments.</typeparam>
        /// <param name="procedure">The procedure instance.</param>
        /// <returns>Allocated list of arguments.</returns>
        public static A ArgList<A>(this Procedure<A> procedure)
            where A : struct
            => new A();

        /// <summary>
        /// Allocates list of arguments on the stack.
        /// </summary>
        /// <typeparam name="T">Type of explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="A">The type representing list of arguments.</typeparam>
        /// <param name="procedure">The procedure instance.</param>
        /// <returns>Allocated list of arguments.</returns>
        public static A ArgList<T, A>(this Procedure<T, A> procedure)
            where A : struct
            => new A();

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        public static void Invoke<T>(this Procedure<T, ValueTuple> procedure, in T instance)
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
        /// <typeparam name="P">The type of the first function argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg">The first procedure argument.</param>
        public static void Invoke<T, P>(this Procedure<T, ValueTuple<P>> procedure, in T instance, P arg)
            => procedure(in instance, new ValueTuple<P>(arg));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="P">The type of the first procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg">The first procedure argument.</param>
        public static void Invoke<P>(this Procedure<ValueTuple<P>> procedure, P arg)
            => procedure(new ValueTuple<P>(arg));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        public static void Invoke<T, P1, P2>(this Procedure<T, (P1, P2)> procedure, in T instance, P1 arg1, P2 arg2)
            => procedure(in instance, (arg1, arg2));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        public static void Invoke<P1, P2>(this Procedure<(P1, P2)> procedure, P1 arg1, P2 arg2)
            => procedure((arg1, arg2));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        public static void Invoke<T, P1, P2, P3>(this Procedure<T, (P1, P2, P3)> procedure, in T instance, P1 arg1, P2 arg2, P3 arg3)
            => procedure(in instance, (arg1, arg2, arg3));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        public static void Invoke<P1, P2, P3>(this Procedure<(P1, P2, P3)> procedure, P1 arg1, P2 arg2, P3 arg3)
            => procedure((arg1, arg2, arg3));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        public static void Invoke<T, P1, P2, P3, P4>(this Procedure<T, (P1, P2, P3, P4)> procedure, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4)
            => procedure(in instance, (arg1, arg2, arg3, arg4));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        public static void Invoke<P1, P2, P3, P4>(this Procedure<(P1, P2, P3, P4)> procedure, P1 arg1, P2 arg2, P3 arg3, P4 arg4)
            => procedure((arg1, arg2, arg3, arg4));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        public static void Invoke<T, P1, P2, P3, P4, P5>(this Procedure<T, (P1, P2, P3, P4, P5)> procedure, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5)
            => procedure(in instance, (arg1, arg2, arg3, arg4, arg5));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        public static void Invoke<P1, P2, P3, P4, P5>(this Procedure<(P1, P2, P3, P4, P5)> procedure, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5)
            => procedure((arg1, arg2, arg3, arg4, arg5));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        public static void Invoke<T, P1, P2, P3, P4, P5, P6>(this Procedure<T, (P1, P2, P3, P4, P5, P6)> procedure, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6)
            => procedure(in instance, (arg1, arg2, arg3, arg4, arg5, arg6));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        public static void Invoke<P1, P2, P3, P4, P5, P6>(this Procedure<(P1, P2, P3, P4, P5, P6)> procedure, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6)
            => procedure((arg1, arg2, arg3, arg4, arg5, arg6));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="P7">The type of the seventh procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="instance">Explicit <c>this</c> argument.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        /// <param name="arg7">The seventh procedure argument.</param>
        public static void Invoke<T, P1, P2, P3, P4, P5, P6, P7>(this Procedure<T, (P1, P2, P3, P4, P5, P6, P7)> procedure, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7)
            => procedure(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="P7">The type of the seventh procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        /// <param name="arg7">The seventh procedure argument.</param>
        public static void Invoke<P1, P2, P3, P4, P5, P6, P7>(this Procedure<(P1, P2, P3, P4, P5, P6, P7)> procedure, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7)
            => procedure((arg1, arg2, arg3, arg4, arg5, arg6, arg7));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="P7">The type of the seventh procedure argument.</typeparam>
        /// <typeparam name="P8">The type of the eighth procedure argument.</typeparam>
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
        public static void Invoke<T, P1, P2, P3, P4, P5, P6, P7, P8>(this Procedure<T, (P1, P2, P3, P4, P5, P6, P7, P8)> procedure, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8)
            => procedure(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="P7">The type of the seventh procedure argument.</typeparam>
        /// <typeparam name="P8">The type of the eighth procedure argument.</typeparam>
        /// <param name="procedure">The procedure to be invoked.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        /// <param name="arg7">The seventh procedure argument.</param>
        /// <param name="arg8">The eighth procedure argument.</param>
        public static void Invoke<P1, P2, P3, P4, P5, P6, P7, P8>(this Procedure<(P1, P2, P3, P4, P5, P6, P7, P8)> procedure, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8)
            => procedure((arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="P7">The type of the seventh procedure argument.</typeparam>
        /// <typeparam name="P8">The type of the eighth procedure argument.</typeparam>
        /// <typeparam name="P9">The type of the ninth procedure argument.</typeparam>
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
        public static void Invoke<T, P1, P2, P3, P4, P5, P6, P7, P8, P9>(this Procedure<T, (P1, P2, P3, P4, P5, P6, P7, P8, P9)> procedure, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9)
            => procedure(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="P7">The type of the seventh procedure argument.</typeparam>
        /// <typeparam name="P8">The type of the eighth procedure argument.</typeparam>
        /// <typeparam name="P9">The type of the ninth procedure argument.</typeparam>
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
        public static void Invoke<P1, P2, P3, P4, P5, P6, P7, P8, P9>(this Procedure<(P1, P2, P3, P4, P5, P6, P7, P8, P9)> procedure, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9)
            => procedure((arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="T">The type of the explicit <c>this</c> argument.</typeparam>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="P7">The type of the seventh procedure argument.</typeparam>
        /// <typeparam name="P8">The type of the eighth procedure argument.</typeparam>
        /// <typeparam name="P9">The type of the ninth procedure argument.</typeparam>
        /// <typeparam name="P10">The type of the tenth procedure argument.</typeparam>
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
        public static void Invoke<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>(this Procedure<T, (P1, P2, P3, P4, P5, P6, P7, P8, P9, P10)> procedure, in T instance, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9, P10 arg10)
            => procedure(in instance, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));

        /// <summary>
        /// Invokes procedure.
        /// </summary>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="P7">The type of the seventh procedure argument.</typeparam>
        /// <typeparam name="P8">The type of the eighth procedure argument.</typeparam>
        /// <typeparam name="P9">The type of the ninth procedure argument.</typeparam>
        /// <typeparam name="P10">The type of the tenth procedure argument.</typeparam>
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
        public static void Invoke<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>(this Procedure<(P1, P2, P3, P4, P5, P6, P7, P8, P9, P10)> procedure, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9, P10 arg10)
            => procedure((arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));
    }
}
