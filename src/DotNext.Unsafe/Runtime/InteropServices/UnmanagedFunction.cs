using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using CallSiteDescr = InlineIL.StandAloneMethodSig;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// Allows to call unmanaged functions with <see langword="void"/> return type.
    /// </summary>
    public static class UnmanagedFunction
    {
        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Cdecl(IntPtr functionPtr)
        {
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(void)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg">The first argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Cdecl<T>(IntPtr functionPtr, T arg)
            where T : unmanaged
        {
            Push(arg);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(void), typeof(T)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Cdecl<T1, T2>(IntPtr functionPtr, T1 arg1, T2 arg2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(void), typeof(T1), typeof(T2)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Cdecl<T1, T2, T3>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(void), typeof(T1), typeof(T2), typeof(T3)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Cdecl<T1, T2, T3, T4>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Cdecl<T1, T2, T3, T4, T5>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <param name="arg6">The sixth argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Cdecl<T1, T2, T3, T4, T5, T6>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(arg6);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <param name="arg6">The sixth argument to be passed into the unmanaged function.</param>
        /// <param name="arg7">The seventh argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Cdecl<T1, T2, T3, T4, T5, T6, T7>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(arg6);
            Push(arg7);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <param name="arg6">The sixth argument to be passed into the unmanaged function.</param>
        /// <param name="arg7">The seventh argument to be passed into the unmanaged function.</param>
        /// <param name="arg8">The eigth argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Cdecl<T1, T2, T3, T4, T5, T6, T7, T8>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(arg6);
            Push(arg7);
            Push(arg8);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <param name="arg6">The sixth argument to be passed into the unmanaged function.</param>
        /// <param name="arg7">The seventh argument to be passed into the unmanaged function.</param>
        /// <param name="arg8">The eigth argument to be passed into the unmanaged function.</param>
        /// <param name="arg9">The eigth argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Cdecl<T1, T2, T3, T4, T5, T6, T7, T8, T9>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(arg6);
            Push(arg7);
            Push(arg8);
            Push(arg9);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StdCall(IntPtr functionPtr)
        {
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(void)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg">The first argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StdCall<T>(IntPtr functionPtr, T arg)
            where T : unmanaged
        {
            Push(arg);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(void), typeof(T)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StdCall<T1, T2>(IntPtr functionPtr, T1 arg1, T2 arg2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(void), typeof(T1), typeof(T2)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StdCall<T1, T2, T3>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(void), typeof(T1), typeof(T2), typeof(T3)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StdCall<T1, T2, T3, T4>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StdCall<T1, T2, T3, T4, T5>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <param name="arg6">The sixth argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StdCall<T1, T2, T3, T4, T5, T6>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(arg6);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <param name="arg6">The sixth argument to be passed into the unmanaged function.</param>
        /// <param name="arg7">The seventh argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StdCall<T1, T2, T3, T4, T5, T6, T7>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(arg6);
            Push(arg7);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <param name="arg6">The sixth argument to be passed into the unmanaged function.</param>
        /// <param name="arg7">The seventh argument to be passed into the unmanaged function.</param>
        /// <param name="arg8">The eigth argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StdCall<T1, T2, T3, T4, T5, T6, T7, T8>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(arg6);
            Push(arg7);
            Push(arg8);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8)));
            Ret();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <param name="arg6">The sixth argument to be passed into the unmanaged function.</param>
        /// <param name="arg7">The seventh argument to be passed into the unmanaged function.</param>
        /// <param name="arg8">The eigth argument to be passed into the unmanaged function.</param>
        /// <param name="arg9">The eigth argument to be passed into the unmanaged function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StdCall<T1, T2, T3, T4, T5, T6, T7, T8, T9>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(arg6);
            Push(arg7);
            Push(arg8);
            Push(arg9);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9)));
            Ret();
        }
    }

    /// <summary>
    /// Allows to call unmanaged functions with <typeparamref name="R"/> return type.
    /// </summary>
    /// <typeparam name="R">The return type of the unmanage function.</typeparam>
    public static class UnmanagedFunction<R>
        where R : unmanaged
    {
        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Cdecl(IntPtr functionPtr)
        {
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(R)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg">The first argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Cdecl<T>(IntPtr functionPtr, T arg)
            where T : unmanaged
        {
            Push(arg);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(R), typeof(T)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Cdecl<T1, T2>(IntPtr functionPtr, T1 arg1, T2 arg2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(R), typeof(T1), typeof(T2)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Cdecl<T1, T2, T3>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(R), typeof(T1), typeof(T2), typeof(T3)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Cdecl<T1, T2, T3, T4>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Cdecl<T1, T2, T3, T4, T5>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <param name="arg6">The sixth argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Cdecl<T1, T2, T3, T4, T5, T6>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(arg6);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <param name="arg6">The sixth argument to be passed into the unmanaged function.</param>
        /// <param name="arg7">The seventh argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Cdecl<T1, T2, T3, T4, T5, T6, T7>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(arg6);
            Push(arg7);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <param name="arg6">The sixth argument to be passed into the unmanaged function.</param>
        /// <param name="arg7">The seventh argument to be passed into the unmanaged function.</param>
        /// <param name="arg8">The eigth argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Cdecl<T1, T2, T3, T4, T5, T6, T7, T8>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(arg6);
            Push(arg7);
            Push(arg8);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with CDECL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <param name="arg6">The sixth argument to be passed into the unmanaged function.</param>
        /// <param name="arg7">The seventh argument to be passed into the unmanaged function.</param>
        /// <param name="arg8">The eigth argument to be passed into the unmanaged function.</param>
        /// <param name="arg9">The eigth argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R Cdecl<T1, T2, T3, T4, T5, T6, T7, T8, T9>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(arg6);
            Push(arg7);
            Push(arg8);
            Push(arg9);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.Cdecl, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R StdCall(IntPtr functionPtr)
        {
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(R)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg">The first argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R StdCall<T>(IntPtr functionPtr, T arg)
            where T : unmanaged
        {
            Push(arg);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(R), typeof(T)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R StdCall<T1, T2>(IntPtr functionPtr, T1 arg1, T2 arg2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(R), typeof(T1), typeof(T2)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R StdCall<T1, T2, T3>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(R), typeof(T1), typeof(T2), typeof(T3)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R StdCall<T1, T2, T3, T4>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R StdCall<T1, T2, T3, T4, T5>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <param name="arg6">The sixth argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R StdCall<T1, T2, T3, T4, T5, T6>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(arg6);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <param name="arg6">The sixth argument to be passed into the unmanaged function.</param>
        /// <param name="arg7">The seventh argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R StdCall<T1, T2, T3, T4, T5, T6, T7>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(arg6);
            Push(arg7);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <param name="arg6">The sixth argument to be passed into the unmanaged function.</param>
        /// <param name="arg7">The seventh argument to be passed into the unmanaged function.</param>
        /// <param name="arg8">The eigth argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R StdCall<T1, T2, T3, T4, T5, T6, T7, T8>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(arg6);
            Push(arg7);
            Push(arg8);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8)));
            return Return<R>();
        }

        /// <summary>
        /// Invokes unmanaged function with STDCALL calling convention by its pointer.
        /// </summary>
        /// <param name="functionPtr">The pointer to the unmanaged function.</param>
        /// <param name="arg1">The first argument to be passed into the unmanaged function.</param>
        /// <param name="arg2">The second argument to be passed into the unmanaged function.</param>
        /// <param name="arg3">The third argument to be passed into the unmanaged function.</param>
        /// <param name="arg4">The fourth argument to be passed into the unmanaged function.</param>
        /// <param name="arg5">The fifth argument to be passed into the unmanaged function.</param>
        /// <param name="arg6">The sixth argument to be passed into the unmanaged function.</param>
        /// <param name="arg7">The seventh argument to be passed into the unmanaged function.</param>
        /// <param name="arg8">The eigth argument to be passed into the unmanaged function.</param>
        /// <param name="arg9">The eigth argument to be passed into the unmanaged function.</param>
        /// <returns>The value returned by unmanaged function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R StdCall<T1, T2, T3, T4, T5, T6, T7, T8, T9>(IntPtr functionPtr, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
        {
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(arg6);
            Push(arg7);
            Push(arg8);
            Push(arg9);
            Push(functionPtr);
            Calli(new CallSiteDescr(CallingConvention.StdCall, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9)));
            return Return<R>();
        }
    }
}
