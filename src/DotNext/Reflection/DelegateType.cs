using System;
using System.Reflection;

namespace DotNext.Reflection
{
    /// <summary>
    /// Provides specialized reflection methods for
    /// delegate types. 
    /// </summary>
    public static class DelegateType
    {
        private const string InvokeMethodName = "Invoke";

        private static MethodInfo GetInvokeMethod(this Type delegateType)
            => !(delegateType is null) && typeof(Delegate).IsAssignableFrom(delegateType) ?
             delegateType.GetMethod(InvokeMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly) :
             null;

        /// <summary>
        /// Returns special Invoke method generate for each delegate type.
        /// </summary>
        /// <typeparam name="D">Type of delegate.</typeparam>
        /// <returns>An object representing reflected method Invoke.</returns>
        public static MethodInfo GetInvokeMethod<D>()
            where D : Delegate
            => typeof(D).GetInvokeMethod();
    }
}