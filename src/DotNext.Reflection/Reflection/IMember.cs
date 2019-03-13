using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Reflection
{
	/// <summary>
	/// Basic interface for all reflected members.
	/// </summary>
	/// <typeparam name="M">Type of reflected member.</typeparam>
	public interface IMember<out M>: ICustomAttributeProvider
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
	public interface IMember<out M, out D>: IMember<M>
		where M: MemberInfo
		where D: Delegate
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R Invoke<M, A, R>(this IMember<M, Function<A, R>> member, in A arguments)
			where M: MemberInfo
			where A : struct
			=> member.Invoker(in arguments);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R Invoke<M, T, A, R>(this IMember<M, Function<T, A, R>> member, in T @this, in A arguments)
			where M: MemberInfo
			where A : struct
			=> member.Invoker(in @this, in arguments);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R Invoke<M, T, R>(this IMember<M, Function<T, ValueTuple, R>> member, in T @this)
			where M: MemberInfo
			=> member.Invoke(in @this, in EmptyTuple.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R Invoke<M, R>(this IMember<M, Function<ValueTuple, R>> member)
			where M: MemberInfo
			=> member.Invoke(in EmptyTuple.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static A ArgList<M, T, A, R>(this IMember<M, Function<T, A, R>> member)
			where M: MemberInfo
			where A : struct
			=> member.Invoker.ArgList();

		/// <summary>
		/// Allocates arguments list on the stack.
		/// </summary>
		/// <param name="member">Callable member.</param>
		/// <typeparam name="M">Type of callable member.</typeparam>
		/// <typeparam name="A">Type of arguments list.</typeparam>
		/// <typeparam name="R">Type of function result.</typeparam>
		/// <returns>Allocated list of arguments.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static A ArgList<M, A, R>(this IMember<M, Function<A, R>> member)
			where M: MemberInfo
			where A : struct
			=> member.Invoker.ArgList();

		public static A ArgList<M, T, A>(this IMember<M, Procedure<T, A>> member)
			where M: MemberInfo
			where A : struct
			=> member.Invoker.ArgList();


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static A ArgList<M, A>(this IMember<M, Procedure<A>> member)
			where M: MemberInfo
			where A : struct
			=> member.Invoker.ArgList();

		#region Functions

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R Invoke<M, R>(this IMember<M, Func<R>> member)
			where M: MemberInfo
			=> member.Invoker();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R Invoke<M, P, R>(this IMember<M, Func<P, R>> member, P arg)
			where M: MemberInfo
			=> member.Invoker(arg);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R Invoke<M, P1, P2, R>(this IMember<M, Func<P1, P2, R>> member, P1 arg1, P2 arg2)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R Invoke<M, P1, P2, P3, R>(this IMember<M, Func<P1, P2, P3, R>> member, P1 arg1, P2 arg2, P3 arg3)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2, arg3);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R Invoke<M, P1, P2, P3, P4, R>(this IMember<M, Func<P1, P2, P3, P4, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2, arg3, arg4);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R Invoke<M, P1, P2, P3, P4, P5, R>(this IMember<M, Func<P1, P2, P3, P4, P5, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2, arg3, arg4, arg5);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R Invoke<M, P1, P2, P3, P4, P5, P6, R>(this IMember<M, Func<P1, P2, P3, P4, P5, P6, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R Invoke<M, P1, P2, P3, P4, P5, P6, P7, R>(this IMember<M, Func<P1, P2, P3, P4, P5, P6, P7, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R Invoke<M, P1, P2, P3, P4, P5, P6, P7, P8, R>(this IMember<M, Func<P1, P2, P3, P4, P5, P6, P7, P8, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R Invoke<M, P1, P2, P3, P4, P5, P6, P7, P8, P9, R>(this IMember<M, Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R Invoke<M, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, R>(this IMember<M, Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9, P10 arg10)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
		#endregion

		#region Actions
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Invoke<M>(this IMember<M, Action> member)
			where M: MemberInfo
			=> member.Invoker();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Invoke<M, P>(this IMember<M, Action<P>> member, P arg)
			where M: MemberInfo
			=> member.Invoker(arg);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Invoke<M, P1, P2>(this IMember<M, Action<P1, P2>> member, P1 arg1, P2 arg2)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Invoke<M, P1, P2, P3>(this IMember<M, Action<P1, P2, P3>> member, P1 arg1, P2 arg2, P3 arg3)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2, arg3);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Invoke<M, P1, P2, P3, P4>(this IMember<M, Action<P1, P2, P3, P4>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2, arg3, arg4);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Invoke<M, P1, P2, P3, P4, P5>(this IMember<M, Action<P1, P2, P3, P4, P5>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2, arg3, arg4, arg5);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Invoke<M, P1, P2, P3, P4, P5, P6>(this IMember<M, Action<P1, P2, P3, P4, P5, P6>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Invoke<M, P1, P2, P3, P4, P5, P6, P7>(this IMember<M, Action<P1, P2, P3, P4, P5, P6, P7>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Invoke<M, P1, P2, P3, P4, P5, P6, P7, P8>(this IMember<M, Action<P1, P2, P3, P4, P5, P6, P7, P8>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Invoke<M, P1, P2, P3, P4, P5, P6, P7, P8, P9>(this IMember<M, Action<P1, P2, P3, P4, P5, P6, P7, P8, P9>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Invoke<M, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>(this IMember<M, Action<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9, P10 arg10)
			where M: MemberInfo
			=> member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
		#endregion

		#region Members
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static V Invoke<M, V>(this IMember<M, MemberGetter<V>> member)
			where M: MemberInfo
			=> member.Invoker();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static V Invoke<M, T, V>(this IMember<M, MemberGetter<T, V>> member, in T @this)
			where M: MemberInfo
			=> member.Invoker(@this);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Invoke<M, V>(this IMember<M, MemberSetter<V>> member, V value)
			where M: MemberInfo
			=> member.Invoker(value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Invoke<M, T, V>(this IMember<M, MemberSetter<T, V>> member, in T @this, V value)
			where M: MemberInfo
			=> member.Invoker(@this, value);

		#endregion
	}
}