using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Runtime.CompilerServices
{
    internal static class PointerHelpers
    {
        public static unsafe object Wrap<T>(T* ptr)
            where T : unmanaged => Pointer.Box(ptr, typeof(T*));

        public static unsafe T* Unwrap<T>(object ptr)
            where T : unmanaged => (T*)Pointer.Unbox(ptr);

        internal static Expression Wrap(Expression expression)
        {
            Debug.Assert(expression.Type.IsPointer);
            var elementType = expression.Type.GetElementType();
            if (elementType == typeof(void))
                return Expression.Call(typeof(Pointer), nameof(Pointer.Box), Array.Empty<Type>(), expression, Expression.Constant(typeof(void*)));
            return Expression.Call(typeof(PointerHelpers), nameof(Wrap), new[] { elementType }, expression);
        }

        internal static Expression Unwrap(Expression expression, Type expectedType)
        {
            Debug.Assert(expectedType.IsPointer);
            var elementType = expectedType.GetElementType();
            if (elementType == typeof(void))
                return Expression.Call(typeof(Pointer), nameof(Pointer.Unbox), Array.Empty<Type>(), expression);
            return Expression.Call(typeof(PointerHelpers), nameof(Unwrap), new[] { elementType }, expression);
        }
    }
}
