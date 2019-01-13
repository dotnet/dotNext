using System;
using System.Reflection;

namespace DotNext.Reflection
{
    internal static class Types
    {
        private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;

        internal static MethodInfo GetHashCodeMethod(this Type type)
            => type.GetMethod(nameof(object.GetHashCode), PublicInstance);

        internal static string ToGetterName(this string propertyName) => "get_" + propertyName;

        internal static string ToSetterName(this string propertyName) => "set_" + propertyName;

        internal static bool IsImplicitlyConvertibleFrom(this Type type, Type target)
            => type == target || !target.IsValueType && type.IsAssignableFrom(target);
    }
}