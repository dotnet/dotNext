using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Reflection
{
    /// <summary>
    /// Basic interface for all reflected members.
    /// </summary>
    /// <typeparam name="TMember">Type of reflected member.</typeparam>
    public interface IMember<out TMember> : ICustomAttributeProvider
        where TMember : MemberInfo
    {
        /// <summary>
        /// Name of member.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Member metadata.
        /// </summary>
        TMember Metadata { get; }
    }

    /// <summary>
    /// Represents callable program element.
    /// </summary>
    /// <typeparam name="TMember">Type of reflected member.</typeparam>
    /// <typeparam name="TInvoker">Type of delegate.</typeparam>
    public interface IMember<out TMember, out TInvoker> : IMember<TMember>, ISupplier<TInvoker>
        where TMember : MemberInfo
        where TInvoker : Delegate
    {
        /// <summary>
        /// Gets delegate that can be used to invoke member.
        /// </summary>
        TInvoker Invoker { get; }

        /// <inheritdoc/>
        TInvoker ISupplier<TInvoker>.Invoke() => Invoker;
    }

    /// <summary>
    /// Provides extension methods for interface <see cref="IMember{M, D}"/> or <see cref="IMember{M}"/>.
    /// </summary>
    public static class Member
    {
        /// <summary>
        /// Invokes member.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="TArgs">The type representing invocation arguments.</typeparam>
        /// <typeparam name="TResult">The return type.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arguments">Invocation arguments placed onto stack.</param>
        /// <returns>Invocation result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<TMember, TArgs, TResult>(this IMember<TMember, Function<TArgs, TResult>> member, [DisallowNull] in TArgs arguments)
            where TMember : MemberInfo
            where TArgs : struct
            => member.Invoker(in arguments);

        /// <summary>
        /// Invokes instance member.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T">The type whose member will be invoked.</typeparam>
        /// <typeparam name="TArgs">The type representing invocation arguments.</typeparam>
        /// <typeparam name="TResult">The return type.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="this">The object whose member will be invoked.</param>
        /// <param name="arguments">Invocation arguments placed onto stack.</param>
        /// <returns>Invocation result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<TMember, T, TArgs, TResult>(this IMember<TMember, Function<T, TArgs, TResult>> member, [DisallowNull] in T @this, in TArgs arguments)
            where TMember : MemberInfo
            where TArgs : struct
            => member.Invoker(in @this, in arguments);

        /// <summary>
        /// Invokes instance member without arguments.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T">The type of the instance.</typeparam>
        /// <typeparam name="TResult">The return type.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="this"><c>this</c> argument.</param>
        /// <returns>Invocation result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<TMember, T, TResult>(this IMember<TMember, Function<T, ValueTuple, TResult>> member, [DisallowNull] in T @this)
            where TMember : MemberInfo
            => member.Invoke(in @this, default);

        /// <summary>
        /// Invokes member without arguments.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="TResult">The return type.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <returns>Invocation result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult? Invoke<TMember, TResult>(this IMember<TMember, Function<ValueTuple, TResult>> member)
            where TMember : MemberInfo
            => member.Invoke(default);

        /// <summary>
        /// Allocates uninitialized structure for placing arguments.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T">The type of the instance.</typeparam>
        /// <typeparam name="TArgs">The type representing invocation arguments.</typeparam>
        /// <typeparam name="TResult">The return type.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <returns>Allocated structure for placing arguments.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TArgs ArgList<TMember, T, TArgs, TResult>(this IMember<TMember, Function<T, TArgs, TResult>> member)
            where TMember : MemberInfo
            where TArgs : struct
            => member.Invoker.ArgList();

        /// <summary>
        /// Allocates uninitialized structure for placing arguments.
        /// </summary>
        /// <param name="member">Callable member.</param>
        /// <typeparam name="TMember">Type of callable member.</typeparam>
        /// <typeparam name="TArgs">Type of arguments list.</typeparam>
        /// <typeparam name="TResult">Type of function result.</typeparam>
        /// <returns>Allocated list of arguments.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TArgs ArgList<TMember, TArgs, TResult>(this IMember<TMember, Function<TArgs, TResult>> member)
            where TMember : MemberInfo
            where TArgs : struct
            => member.Invoker.ArgList();

        /// <summary>
        /// Allocates uninitialized structure for placing arguments.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T">The type of the instance.</typeparam>
        /// <typeparam name="TArgs">Type of arguments list.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <returns>Allocated list of arguments.</returns>
        public static TArgs ArgList<TMember, T, TArgs>(this IMember<TMember, Procedure<T, TArgs>> member)
            where TMember : MemberInfo
            where TArgs : struct
            => member.Invoker.ArgList();

        /// <summary>
        /// Allocates uninitialized structure for placing arguments.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="TArgs">Type of arguments list.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <returns>Allocated list of arguments.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TArgs ArgList<TMember, TArgs>(this IMember<TMember, Procedure<TArgs>> member)
            where TMember : MemberInfo
            where TArgs : struct
            => member.Invoker.ArgList();

        #region Functions

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="TResult">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <returns>Return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Invoke<TMember, TResult>(this IMember<TMember, Func<TResult>> member)
            where TMember : MemberInfo
            => member.Invoker();

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="TParam">The type of the first function argument.</typeparam>
        /// <typeparam name="TResult">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg">The first function argument.</param>
        /// <returns>Return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Invoke<TMember, TParam, TResult>(this IMember<TMember, Func<TParam, TResult>> member, TParam arg)
            where TMember : MemberInfo
            => member.Invoker(arg);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="TResult">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <returns>Return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Invoke<TMember, T1, T2, TResult>(this IMember<TMember, Func<T1, T2, TResult>> member, T1 arg1, T2 arg2)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="TResult">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <returns>Return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Invoke<TMember, T1, T2, T3, TResult>(this IMember<TMember, Func<T1, T2, T3, TResult>> member, T1 arg1, T2 arg2, T3 arg3)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2, arg3);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="TResult">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <returns>Return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Invoke<TMember, T1, T2, T3, T4, TResult>(this IMember<TMember, Func<T1, T2, T3, T4, TResult>> member, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="TResult">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <returns>Return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Invoke<TMember, T1, T2, T3, T4, T5, TResult>(this IMember<TMember, Func<T1, T2, T3, T4, T5, TResult>> member, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="TResult">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <returns>Return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Invoke<TMember, T1, T2, T3, T4, T5, T6, TResult>(this IMember<TMember, Func<T1, T2, T3, T4, T5, T6, TResult>> member, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="TResult">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <returns>Return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Invoke<TMember, T1, T2, T3, T4, T5, T6, T7, TResult>(this IMember<TMember, Func<T1, T2, T3, T4, T5, T6, T7, TResult>> member, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth function argument.</typeparam>
        /// <typeparam name="TResult">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <param name="arg8">The eighth function argument.</param>
        /// <returns>Return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Invoke<TMember, T1, T2, T3, T4, T5, T6, T7, T8, TResult>(this IMember<TMember, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult>> member, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth function argument.</typeparam>
        /// <typeparam name="T9">The type of the ninth function argument.</typeparam>
        /// <typeparam name="TResult">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <param name="arg8">The eighth function argument.</param>
        /// <param name="arg9">The ninth function argument.</param>
        /// <returns>Return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Invoke<TMember, T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>(this IMember<TMember, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>> member, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);

        /// <summary>
        /// Invokes member as function.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T1">The type of the first function argument.</typeparam>
        /// <typeparam name="T2">The type of the second function argument.</typeparam>
        /// <typeparam name="T3">The type of the third function argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth function argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth function argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth function argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh function argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth function argument.</typeparam>
        /// <typeparam name="T9">The type of the ninth function argument.</typeparam>
        /// <typeparam name="T10">The type of the tenth function argument.</typeparam>
        /// <typeparam name="TResult">Type of function result.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <param name="arg7">The seventh function argument.</param>
        /// <param name="arg8">The eighth function argument.</param>
        /// <param name="arg9">The ninth function argument.</param>
        /// <param name="arg10">The tenth function argument.</param>
        /// <returns>Return value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Invoke<TMember, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(this IMember<TMember, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>> member, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        #endregion

        #region Actions

        /// <summary>
        /// Invokes member as action.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <param name="member">Callable member.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<TMember>(this IMember<TMember, Action> member)
            where TMember : MemberInfo
            => member.Invoker();

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="TParam">The type of the first procedure argument.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg">The first procedure argument.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<TMember, TParam>(this IMember<TMember, Action<TParam>> member, TParam arg)
            where TMember : MemberInfo
            => member.Invoker(arg);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<TMember, T1, T2>(this IMember<TMember, Action<T1, T2>> member, T1 arg1, T2 arg2)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<TMember, T1, T2, T3>(this IMember<TMember, Action<T1, T2, T3>> member, T1 arg1, T2 arg2, T3 arg3)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2, arg3);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<TMember, T1, T2, T3, T4>(this IMember<TMember, Action<T1, T2, T3, T4>> member, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth procedure argument.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<TMember, T1, T2, T3, T4, T5>(this IMember<TMember, Action<T1, T2, T3, T4, T5>> member, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth procedure argument.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<TMember, T1, T2, T3, T4, T5, T6>(this IMember<TMember, Action<T1, T2, T3, T4, T5, T6>> member, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh procedure argument.</typeparam>
        /// <param name="member">Callable member.</param>
        /// <param name="arg1">The first procedure argument.</param>
        /// <param name="arg2">The second procedure argument.</param>
        /// <param name="arg3">The third procedure argument.</param>
        /// <param name="arg4">The fourth procedure argument.</param>
        /// <param name="arg5">The fifth procedure argument.</param>
        /// <param name="arg6">The sixth procedure argument.</param>
        /// <param name="arg7">The seventh procedure argument.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<TMember, T1, T2, T3, T4, T5, T6, T7>(this IMember<TMember, Action<T1, T2, T3, T4, T5, T6, T7>> member, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh procedure argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth procedure argument.</typeparam>
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
        public static void Invoke<TMember, T1, T2, T3, T4, T5, T6, T7, T8>(this IMember<TMember, Action<T1, T2, T3, T4, T5, T6, T7, T8>> member, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T1">The type of the first procedure argument.</typeparam>
        /// <typeparam name="T2">The type of the second procedure argument.</typeparam>
        /// <typeparam name="T3">The type of the third procedure argument.</typeparam>
        /// <typeparam name="T4">The type of the fourth procedure argument.</typeparam>
        /// <typeparam name="T5">The type of the fifth procedure argument.</typeparam>
        /// <typeparam name="T6">The type of the sixth procedure argument.</typeparam>
        /// <typeparam name="T7">The type of the seventh procedure argument.</typeparam>
        /// <typeparam name="T8">The type of the eighth procedure argument.</typeparam>
        /// <typeparam name="T9">The type of the ninth procedure argument.</typeparam>
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
        public static void Invoke<TMember, T1, T2, T3, T4, T5, T6, T7, T8, T9>(this IMember<TMember, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9>> member, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);

        /// <summary>
        /// Invokes member as procedure.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
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
        public static void Invoke<TMember, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this IMember<TMember, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>> member, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
            where TMember : MemberInfo
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        #endregion

        #region Members

        /// <summary>
        /// Gets property or field value.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="TValue">The type of the member value.</typeparam>
        /// <param name="member">The property or field.</param>
        /// <returns>The member value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TValue? Invoke<TMember, TValue>(this IMember<TMember, MemberGetter<TValue>> member)
            where TMember : MemberInfo
            => member.Invoker();

        /// <summary>
        /// Gets property or field value.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T">The type whose property value will be returned.</typeparam>
        /// <typeparam name="TValue">The type of the member value.</typeparam>
        /// <param name="member">The property or field.</param>
        /// <param name="this">The object whose property or field value will be returned.</param>
        /// <returns>The member value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TValue? Invoke<TMember, T, TValue>(this IMember<TMember, MemberGetter<T, TValue>> member, [DisallowNull] in T @this)
            where TMember : MemberInfo
            => member.Invoker(@this);

        /// <summary>
        /// Sets property or field.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="TValue">The type of the member value.</typeparam>
        /// <param name="member">The property or field.</param>
        /// <param name="value">The new value of the field or property.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<TMember, TValue>(this IMember<TMember, MemberSetter<TValue>> member, TValue value)
            where TMember : MemberInfo
            => member.Invoker(value);

        /// <summary>
        /// Sets property or field.
        /// </summary>
        /// <typeparam name="TMember">The type of the member.</typeparam>
        /// <typeparam name="T">The type whose property or field value will be set.</typeparam>
        /// <typeparam name="TValue">The type of the member value.</typeparam>
        /// <param name="member">The property or field.</param>
        /// <param name="this">The object whose property or field value will be set.</param>
        /// <param name="value">The new value of the field or property.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke<TMember, T, TValue>(this IMember<TMember, MemberSetter<T, TValue>> member, [DisallowNull] in T @this, TValue value)
            where TMember : MemberInfo
            => member.Invoker(@this, value);

        #endregion
    }
}