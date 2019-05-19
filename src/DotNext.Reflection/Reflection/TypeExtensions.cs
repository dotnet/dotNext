using System;
using System.Reflection;

namespace DotNext.Reflection
{
    internal static class TypeExtensions
    {
        private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;

        internal static MethodInfo GetHashCodeMethod(this Type type)
            => type.GetMethod(nameof(GetHashCode), PublicInstance, Array.Empty<Type>());

        internal static string ToGetterName(this string propertyName) => string.Concat("get_", propertyName);

        internal static string ToSetterName(this string propertyName) => string.Concat("set_", propertyName);
    }
}