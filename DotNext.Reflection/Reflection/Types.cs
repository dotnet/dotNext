using System;
using System.Reflection;
using System.Linq.Expressions;

namespace DotNext.Reflection
{
    internal static class Types
    {
        private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;

        internal static MethodInfo GetHashCodeMethod(this Type type)
            => type.GetMethod(nameof(object.GetHashCode), PublicInstance);

        internal static string ToGetterName(this string propertyName) => "get_" + propertyName;

        internal static string ToSetterName(this string propertyName) => "set_" + propertyName;

		internal static bool Equals(this Type type, ParameterExpression expression)
			=> type.IsByRef ? (type.GetElementType() == expression.Type && expression.IsByRef) : (type == expression.Type && !expression.IsByRef);
    }
}