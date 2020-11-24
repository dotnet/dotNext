using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;

namespace DotNext.Runtime.CompilerServices
{
    internal static class ReflectionUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe object Wrap<T>(T* ptr)
            where T : unmanaged => Pointer.Box(ptr, typeof(T*));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T* Unwrap<T>(object ptr)
            where T : unmanaged => (T*)Pointer.Unbox(ptr);

        internal static Expression Wrap(Expression expression)
        {
            Debug.Assert(expression.Type.IsPointer);
            if (expression.Type == typeof(void*))
                return Expression.Call(typeof(Pointer), nameof(Pointer.Box), Type.EmptyTypes, expression, Expression.Constant(typeof(void*)));
            return Expression.Call(typeof(ReflectionUtils), nameof(Wrap), new[] { expression.Type.GetElementType() }, expression);
        }

        internal static Expression Unwrap(Expression expression, Type expectedType)
        {
            Debug.Assert(expectedType.IsPointer);
            if (expectedType == typeof(void*))
                return Expression.Call(typeof(Pointer), nameof(Pointer.Unbox), Type.EmptyTypes, expression);
            return Expression.Call(typeof(ReflectionUtils), nameof(Unwrap), new[] { expectedType.GetElementType() }, expression);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T VolatileRead<T>(ref T fieldRef)
        {
            Push(ref fieldRef);
            Volatile();
            Ldobj<T>();
            return Return<T>();
        }

        internal static Expression VolatileRead(Expression expression)
        {
            if (!expression.Type.IsPointer)
                return Expression.Call(typeof(ReflectionUtils), nameof(VolatileRead), new[] { expression.Type }, expression);

            if (expression.Type == typeof(void*))
                return Expression.Call(typeof(ReflectionUtils), nameof(VolatileReadPointer), Type.EmptyTypes, expression);

            return Expression.Call(typeof(ReflectionUtils), nameof(VolatileReadPointer), new[] { expression.Type.GetElementType() }, expression);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T* VolatileReadPointer<T>(ref T* fieldRef)
            where T : unmanaged
        {
            Ldarg(nameof(fieldRef));
            Volatile();
            Ldind_I();
            return ReturnPointer<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void* VolatileReadPointer(ref void* fieldRef)
        {
            Ldarg(nameof(fieldRef));
            Volatile();
            Ldind_I();
            return ReturnPointer();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VolatileWrite<T>(ref T fieldRef, T value)
        {
            Push(ref fieldRef);
            Push(value);
            Volatile();
            Stobj<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void VolatileWritePointer<T>(ref T* fieldRef, T* value)
            where T : unmanaged
        {
            Ldarg(nameof(fieldRef));
            Push(value);
            Volatile();
            Stind_I();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void VolatileWritePointer(ref void* fieldRef, void* value)
        {
            Ldarg(nameof(fieldRef));
            Push(value);
            Volatile();
            Stind_I();
        }

        internal static Expression VolatileWrite(Expression expression, Expression value)
        {
            if (!expression.Type.IsPointer)
                return Expression.Call(typeof(ReflectionUtils), nameof(VolatileWrite), new[] { expression.Type }, expression, value);

            if (expression.Type == typeof(void*))
                return Expression.Call(typeof(ReflectionUtils), nameof(VolatileWritePointer), Type.EmptyTypes, expression, value);

            return Expression.Call(typeof(ReflectionUtils), nameof(VolatileWritePointer), new[] { expression.Type.GetElementType() }, expression, value);
        }
    }
}
