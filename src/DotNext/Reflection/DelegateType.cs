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

        /// <summary>
        /// Returns special Invoke method generate for each delegate type.
        /// </summary>
        /// <typeparam name="D">Type of delegate.</typeparam>
        /// <returns>An object representing reflected method Invoke.</returns>
        /// <exception cref="GenericArgumentException{G}"><typeparamref name="D"/> is not a concrete delegate type.</exception>
        public static MethodInfo GetInvokeMethod<D>()
            where D : Delegate
        {
            var delegateType = typeof(D);
            return delegateType.IsSealed ?
                delegateType.GetMethod(InvokeMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                : throw new GenericArgumentException<D>(ExceptionMessages.ConcreteDelegateExpected);
        }
    }
}