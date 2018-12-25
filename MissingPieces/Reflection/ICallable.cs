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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Invoke<A, R>(this ICallable<Function<A, R>> member, in A arguments)
            where A: struct
            => member.Invoker(in arguments);

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

    }
}