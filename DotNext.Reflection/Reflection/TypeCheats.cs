using System;
using System.Reflection;

namespace DotNext.Reflection
{
    internal static class TypeCheats
    {
        private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;

        internal static MethodInfo GetHashCodeMethod(this Type type)
            => type.GetMethod(nameof(object.GetHashCode), PublicInstance);
    }
}