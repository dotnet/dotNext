using System;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Reflection
{
    internal static class TypeExtensions
    {
        private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;

        internal static MethodInfo GetHashCodeMethod(this Type type)
            => type.GetMethod(nameof(object.GetHashCode), PublicInstance, Array.Empty<Type>());

        internal static string ToGetterName(this string propertyName) => string.Concat("get_", propertyName);

        internal static string ToSetterName(this string propertyName) => string.Concat("set_", propertyName);

        internal static bool Equals(this Type type, ParameterExpression expression)
            => type.IsByRef ? (type.GetElementType() == expression.Type && expression.IsByRef) : (type == expression.Type && !expression.IsByRef);
    }
}