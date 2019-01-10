using System;
using System.Reflection;
using System.Security;

namespace Cheats.Reflection
{
    internal static class Ref
    {
        private static bool Is(Type type) => type.IsGenericInstanceOf(typeof(Ref<>));

        internal static bool Reflect(Type byRefType, out Type underlyingType, out FieldInfo valueField)
        {
            if(Is(byRefType))
            {
                underlyingType = byRefType.GetGenericArguments()[0];
                valueField = byRefType.GetField("Value", BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance);
                return true;
            }
            else
            {
                underlyingType = null;
                valueField = null;
                return false;
            }
        }
    }

    /// <summary>
    /// Wrapper for by-ref argument.
    /// </summary>
    /// <typeparam name="T">Referenced type.</typeparam>
    [SecuritySafeCritical]
    public struct Ref<T>
    {
        /// <summary>
        /// Gets or sets value.
        /// </summary>
        [SecuritySafeCritical]
        internal T Value;

        public static implicit operator T(Ref<T> reference) => reference.Value;

        public static implicit operator Ref<T>(T value) => new Ref<T> { Value = value };
    }
}