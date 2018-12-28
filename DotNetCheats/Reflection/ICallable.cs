using System;
using System.Runtime.CompilerServices;

namespace MissingPieces.Reflection
{
    /// <summary>
    /// Represents callable program element.
    /// </summary>
    /// <typeparam name="D">Type of delegate.</typeparam>
    public interface ICallable<out D>
        where D: Delegate
    {   
        /// <summary>
        /// Gets delegate that can be used to invoke member.
        /// </summary>
        D Invoker { get; }
    }

    public static class Callable
    {
		private static readonly ValueTuple EmptyTuple = new ValueTuple();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<A, R>(this ICallable<Function<A, R>> member, in A arguments)
            where A: struct
            => member.Invoker(in arguments);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<T, A, R>(this ICallable<Function<T, A, R>> member, in T @this, in A arguments)
            where A: struct
            => member.Invoker(in @this, in arguments);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R Invoke<T, R>(this ICallable<Function<T, ValueTuple, R>> member, in T @this)
			=> member.Invoke(in @this, in EmptyTuple);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R Invoke<R>(this ICallable<Function<ValueTuple, R>> member)
			=> member.Invoke(in EmptyTuple);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static A ArgList<T, A, R>(this ICallable<Function<T, A, R>> member)
            where A: struct
            => new A();

        /// <summary>
        /// Allocates arguments list on the stack.
        /// </summary>
        /// <param name="member">Callable member.</param>
        /// <typeparam name="A">Type of arguments list.</typeparam>
        /// <typeparam name="R">Type of function result.</typeparam>
        /// <returns>Allocated list of arguments.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static A ArgList<A, R>(this ICallable<Function<A, R>> member)
            where A: struct
            => new A();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<R>(this ICallable<Func<R>> member) 
            => member.Invoker();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<P, R>(this ICallable<Func<P, R>> member, P arg) 
            => member.Invoker(arg);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<P1, P2, R>(this ICallable<Func<P1, P2, R>> member, P1 arg1, P2 arg2) 
            => member.Invoker(arg1, arg2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<P1, P2, P3, R>(this ICallable<Func<P1, P2, P3, R>> member, P1 arg1, P2 arg2, P3 arg3) 
            => member.Invoker(arg1, arg2, arg3);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<P1, P2, P3, P4, R>(this ICallable<Func<P1, P2, P3, P4, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4) 
            => member.Invoker(arg1, arg2, arg3, arg4);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<P1, P2, P3, P4, P5, R>(this ICallable<Func<P1, P2, P3, P4, P5, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5) 
            => member.Invoker(arg1, arg2, arg3, arg4, arg5);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<P1, P2, P3, P4, P5, P6, R>(this ICallable<Func<P1, P2, P3, P4, P5, P6, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6) 
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<P1, P2, P3, P4, P5, P6, P7, R>(this ICallable<Func<P1, P2, P3, P4, P5, P6, P7, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7) 
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]            
        public static R Invoke<P1, P2, P3, P4, P5, P6, P7, P8, R>(this ICallable<Func<P1, P2, P3, P4, P5, P6, P7, P8, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8) 
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<P1, P2, P3, P4, P5, P6, P7, P8, P9, R>(this ICallable<Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9) 
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, R>(this ICallable<Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, R>> member, P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9, P10 arg10) 
            => member.Invoker(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
    }
}