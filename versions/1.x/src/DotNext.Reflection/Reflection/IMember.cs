using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Reflection
{
    /// <summary>
    /// Basic interface for all reflected members.
    /// </summary>
    /// <typeparam name="M">Type of reflected member.</typeparam>
    public interface IMember<out M> : ICustomAttributeProvider
        where M : MemberInfo
    {
        /// <summary>
        /// Name of member.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Member metadata.
        /// </summary>
        M RuntimeMember { get; }
    }

    /// <summary>
    /// Represents callable program element.
    /// </summary>
    /// <typeparam name="M">Type of reflected member.</typeparam>
    /// <typeparam name="D">Type of delegate.</typeparam>
    public interface IMember<out M, out D> : IMember<M>
        where M : MemberInfo
        where D : Delegate
    {
        /// <summary>
        /// Gets delegate that can be used to invoke member.
        /// </summary>
        D Invoker { get; }
    }

    /// <summary>
    /// Provides extension methods for interface <see cref="IMember{M, D}"/> or <see cref="IMember{M}"/>.
    /// </summary>
	public static class Member
    {
        /// <summary>
        /// Invokes member.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="A">The type representing invocation arguments.</typeparam>
        /// <typeparam name="R">The return type.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arguments">Invocation arguments placed onto stack.</param>
        /// <returns>Invocation result.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<M, A, R>(this IMember<M, Function<A, R>> member, in A arguments)
            where M : MemberInfo
            where A : struct
            => member.Invoker(in arguments);

        /// <summary>
        /// Invokes instance member.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="T">The type whose member will be invoked.</typeparam>
        /// <typeparam name="A">The type representing invocation arguments.</typeparam>
        /// <typeparam name="R">The return type.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="this">The object whose member will be invoked.</param>
        /// <param name="arguments">Invocation arguments placed onto stack.</param>
        /// <returns>Invocation result.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<M, T, A, R>(this IMember<M, Function<T, A, R>> member, in T @this, in A arguments)
            where M : MemberInfo
            where A : struct
            => member.Invoker(in @this, in arguments);

        /// <summary>
        /// Invokes instance member without arguments.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="T">The type of the instance.</typeparam>
        /// <typeparam name="R">The return type.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="this"><c>this</c> argument.</param>
        /// <returns>Invocation result.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<M, T, R>(this IMember<M, Function<T, ValueTuple, R>> member, in T @this)
            where M : MemberInfo
            => member.Invoke(in @this, default);

        /// <summary>
        /// Invokes member without arguments.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="R">The return type.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <returns>Invocation result.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<M, R>(this IMember<M, Function<ValueTuple, R>> member)
            where M : MemberInfo
            => member.Invoke(default);

        /// <summary>
        /// Allocates uninitialized structure for placing arguments.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="T">The type of the instance.</typeparam>
        /// <typeparam name="A">The type representing invocation arguments.</typeparam>
        /// <typeparam name="R">The return type.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <returns>Allocated structure for placing arguments.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static A ArgList<M, T, A, R>(this IMember<M, Function<T, A, R>> member)
            where M : MemberInfo
            where A : struct
            => member.Invoker.ArgList();

        /// <summary>
        /// Allocates uninitialized structure for placing arguments.
        /// </summary>
        /// <param name="member">Callable member.</param>
        /// <typeparam name="M">Type of callable member.</typeparam>
        /// <typeparam name="A">Type of arguments list.</typeparam>
        /// <typeparam name="R">Type of function result.</typeparam>
        /// <returns>Allocated list of arguments.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static A ArgList<M, A, R>(this IMember<M, Function<A, R>> member)
            where M : MemberInfo
            where A : struct
            => member.Invoker.ArgList();

        /// <summary>
        /// Allocates uninitialized structure for placing arguments.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="T">The type of the instance.</typeparam>
        /// <typeparam name="A">Type of arguments list.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <returns>Allocated list of arguments.</returns>
		public static A ArgList<M, T, A>(this IMember<M, Procedure<T, A>> member)
            where M : MemberInfo
            where A : struct
            => member.Invoker.ArgList();

        /// <summary>
        /// Allocates uninitialized structure for placing arguments.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="A">Type of arguments list.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <returns>Allocated list of arguments.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static A ArgList<M, A>(this IMember<M, Procedure<A>> member)
            where M : MemberInfo
            where A : struct
            => member.Invoker.ArgList();

        #region Functions

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="R">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <returns>Return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<M, R>(this IMember<M, Func<R>> member)
            where M : MemberInfo
            => member.Invoker();

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P">The type of the first function argument.</typeparam>
        /// <typeparam name="R">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg">The first function argument.</param>
        /// <returns>Return value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<M, P, R>(this IMember<M, Func<P, R>> member, P arg)
            where M : MemberInfo
            => member.Invoker(arg);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P1">The type of the first function argument.</typeparam>
        /// <typeparam name="P2">The type of the second function argument.</typeparam>
        /// <typeparam name="R">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <returns>Return value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<M, P1, P2, R>(this IMember<M, Func<P1, P2, R>> member, P1 arg1, P2 arg2)
            where M : MemberInfo
            => member.Invoker(arg1, arg2);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P1">The type of the first function argument.</typeparam>
        /// <typeparam name="P2">The type of the second function argument.</typeparam>
        /// <typeparam name="P3">The type of the third function argument.</typeparam>
        /// <typeparam name="R">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <returns>Return value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<M, P1, P2, P3, R>(this IMember<M, Func<P1, P2, P3, R>> member, P1 arg1, P2 arg2, P3 arg3)
            where M : MemberInfo
            => member.Invoker(arg1, arg2, arg3);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P1">The type of the first function argument.</typeparam>
        /// <typeparam name="P2">The type of the second function argument.</typeparam>
        /// <typeparam name="P3">The type of the third function argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="R">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <returns>Return value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<M, P1, P2, P3, P4, R>(this IMember<M, Func<P1, P2, P3, P4, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4)
            where M : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P1">The type of the first function argument.</typeparam>
        /// <typeparam name="P2">The type of the second function argument.</typeparam>
        /// <typeparam name="P3">The type of the third function argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="R">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <returns>Return value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<M, P1, P2, P3, P4, P5, R>(this IMember<M, Func<P1, P2, P3, P4, P5, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5)
            where M : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P1">The type of the first function argument.</typeparam>
        /// <typeparam name="P2">The type of the second function argument.</typeparam>
        /// <typeparam name="P3">The type of the third function argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="R">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The fifth function argument.</param>
        /// <returns>Return value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<M, P1, P2, P3, P4, P5, P6, R>(this IMember<M, Func<P1, P2, P3, P4, P5, P6, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6)
            where M : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P1">The type of the first function argument.</typeparam>
        /// <typeparam name="P2">The type of the second function argument.</typeparam>
        /// <typeparam name="P3">The type of the third function argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="P7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="R">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The fifth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <returns>Return value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<M, P1, P2, P3, P4, P5, P6, P7, R>(this IMember<M, Func<P1, P2, P3, P4, P5, P6, P7, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7)
            where M : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P1">The type of the first function argument.</typeparam>
        /// <typeparam name="P2">The type of the second function argument.</typeparam>
        /// <typeparam name="P3">The type of the third function argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="P7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="P8">The type of the eighth function argument.</typeparam>
        /// <typeparam name="R">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The fifth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <param name="arg8">The eighth function argument.</param>
        /// <returns>Return value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<M, P1, P2, P3, P4, P5, P6, P7, P8, R>(this IMember<M, Func<P1, P2, P3, P4, P5, P6, P7, P8, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8)
            where M : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P1">The type of the first function argument.</typeparam>
        /// <typeparam name="P2">The type of the second function argument.</typeparam>
        /// <typeparam name="P3">The type of the third function argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="P7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="P8">The type of the eighth function argument.</typeparam>
        /// <typeparam name="P9">The type of the ninth function argument.</typeparam>
        /// <typeparam name="R">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The fifth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <param name="arg8">The eighth function argument.</param>
        /// <param name="arg9">The ninth function argument.</param>
        /// <returns>Return value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<M, P1, P2, P3, P4, P5, P6, P7, P8, P9, R>(this IMember<M, Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9)
            where M : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P1">The type of the first function argument.</typeparam>
        /// <typeparam name="P2">The type of the second function argument.</typeparam>
        /// <typeparam name="P3">The type of the third function argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="P7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="P8">The type of the eighth function argument.</typeparam>
        /// <typeparam name="P9">The type of the ninth function argument.</typeparam>
        /// <typeparam name="P10">The type of the tenth function argument.</typeparam>
        /// <typeparam name="R">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The fifth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <param name="arg8">The eighth function argument.</param>
        /// <param name="arg9">The ninth function argument.</param>
        /// <param name="arg10">The tenth function argument.</param>
        /// <returns>Return value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<M, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, R>(this IMember<M, Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9, P10 arg10)
            where M : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        #endregion

        #region Actions

        /// <summary>
        /// Invokes member as action.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <param name="member">Callable member.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<M>(this IMember<M, Action> member)
            where M : MemberInfo
            => member.Invoker();

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P">The type of the first procedure argument.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg">The first procedure argument.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<M, P>(this IMember<M, Action<P>> member, P arg)
            where M : MemberInfo
            => member.Invoker(arg);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<M, P1, P2>(this IMember<M, Action<P1, P2>> member, P1 arg1, P2 arg2)
            where M : MemberInfo
            => member.Invoker(arg1, arg2);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<M, P1, P2, P3>(this IMember<M, Action<P1, P2, P3>> member, P1 arg1, P2 arg2, P3 arg3)
            where M : MemberInfo
            => member.Invoker(arg1, arg2, arg3);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<M, P1, P2, P3, P4>(this IMember<M, Action<P1, P2, P3, P4>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4)
            where M : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth procedure argument.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<M, P1, P2, P3, P4, P5>(this IMember<M, Action<P1, P2, P3, P4, P5>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5)
            where M : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth procedure argument.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<M, P1, P2, P3, P4, P5, P6>(this IMember<M, Action<P1, P2, P3, P4, P5, P6>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6)
            where M : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="P7">The type of the seventh procedure argument.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        /// <param name="arg7">The seventh procedure argument.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<M, P1, P2, P3, P4, P5, P6, P7>(this IMember<M, Action<P1, P2, P3, P4, P5, P6, P7>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7)
            where M : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="P7">The type of the seventh procedure argument.</typeparam>
        /// <typeparam name="P8">The type of the eighth procedure argument.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        /// <param name="arg7">The seventh procedure argument.</param>
        /// <param name="arg8">The eighth procedure argument.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<M, P1, P2, P3, P4, P5, P6, P7, P8>(this IMember<M, Action<P1, P2, P3, P4, P5, P6, P7, P8>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8)
            where M : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="P1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="P2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="P3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="P4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="P5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="P6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="P7">The type of the seventh procedure argument.</typeparam>
        /// <typeparam name="P8">The type of the eighth procedure argument.</typeparam>
        /// <typeparam name="P9">The type of the ninth procedure argument.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        /// <param name="arg7">The seventh procedure argument.</param>
        /// <param name="arg8">The eighth procedure argument.</param>
        /// <param name="arg9">The ninth procedure argument.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<M, P1, P2, P3, P4, P5, P6, P7, P8, P9>(this IMember<M, Action<P1, P2, P3, P4, P5, P6, P7, P8, P9>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9)
            where M : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
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
        /// <param name="member">Callable member.</param>
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<M, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>(this IMember<M, Action<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9, P10 arg10)
            where M : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        #endregion

        #region Members

        /// <summary>
        /// Gets property or field value.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="V">The type of the member value.</typeparam>
        /// <param name="member">The property or field.</param>
        /// <returns>The member value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static V Invoke<M, V>(this IMember<M, MemberGetter<V>> member)
            where M : MemberInfo
            => member.Invoker();

        /// <summary>
        /// Gets property or field value.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="T">The type whose property value will be returned.</typeparam>
        /// <typeparam name="V">The type of the member value.</typeparam>
        /// <param name="member">The property or field.</param>
        /// <param name="this">The object whose property or field value will be returned.</param>
        /// <returns>The member value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static V Invoke<M, T, V>(this IMember<M, MemberGetter<T, V>> member, in T @this)
            where M : MemberInfo
            => member.Invoker(@this);

        /// <summary>
        /// Sets property or field.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="V">The type of the member value.</typeparam>
        /// <param name="member">The property or field.</param>
        /// <param name="value">The new value of the field or property.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<M, V>(this IMember<M, MemberSetter<V>> member, V value)
            where M : MemberInfo
            => member.Invoker(value);

        /// <summary>
        /// Sets property or field.
        /// </summary>
        /// <typeparam name="M">The type of the member.</typeparam>
        /// <typeparam name="T">The type whose property or field value will be set.</typeparam>
        /// <typeparam name="V">The type of the member value.</typeparam>
        /// <param name="member">The property or field.</param>
        /// <param name="this">The object whose property or field value will be set.</param>
        /// <param name="value">The new value of the field or property.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<M, T, V>(this IMember<M, MemberSetter<T, V>> member, in T @this, V value)
            where M : MemberInfo
            => member.Invoker(@this, value);

        #endregion
    }
}