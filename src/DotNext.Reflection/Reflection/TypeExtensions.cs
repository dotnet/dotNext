using System;
using System.Reflection;

namespace DotNext.Reflection
{
    internal static class TypeExtensions
    {
        private const BindingFlags PublicInstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;

        internal static MethodInfo GetHashCodeMethod(this Type type)
            => type.GetMethod(nameof(GetHashCode), PublicInstanceFlags, null, Array.Empty<Type>(), Array.Empty<ParameterModifier>());

        internal static string ToGetterName(this string propertyName) => string.Concat("get_", propertyName);

        internal static string ToSetterName(this string propertyName) => string.Concat("set_", propertyName);

        internal static Type NonRefType(this Type type) => type.IsByRef ? type.GetElementType() : type;
    }
}