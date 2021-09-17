using System.Diagnostics;
using System.Reflection;

namespace DotNext.Reflection;

internal static class TypeExtensions
{
    private const BindingFlags PublicInstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;

    internal static MethodInfo? GetHashCodeMethod(this Type type)
        => type.GetMethod(nameof(GetHashCode), PublicInstanceFlags, null, Type.EmptyTypes, null);

    internal static string ToGetterName(this string propertyName) => string.Concat("get_", propertyName);

    internal static string ToSetterName(this string propertyName) => string.Concat("set_", propertyName);

    internal static Type NonRefType(this Type type)
    {
        if (type.IsByRef)
        {
            var result = type.GetElementType();
            Debug.Assert(result is not null);
            return result;
        }

        return type;
    }
}