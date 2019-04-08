using System;
using System.Reflection;

namespace DotNext.Reflection
{
    /// <summary>
    /// Various extension methods for method reflection.
    /// </summary>
    public static class Method
    {
        /// <summary>
        /// Returns method parameter types respecting orded of parameters.
        /// </summary>
        /// <param name="method">The method to reflect.</param>
        /// <returns>The array of parameter types.</returns>
		public static Type[] GetParameterTypes(this MethodBase method)
            => method is null ? null : Array.ConvertAll(method.GetParameters(), p => p.ParameterType);

        /// <summary>
        /// Determines whether the method parameters have the same set of types as in given array of types.
        /// </summary>
        /// <param name="method">The method to check.</param>
        /// <param name="parameters">The expected parameter types.</param>
        /// <returns><see langword="true"/>, if the method parameters have the same set of types as types passed as array; otherwise, <see langword="false"/>.</returns>
		public static bool SignatureEquals(this MethodBase method, Type[] parameters)
        {
            var firstParams = method.GetParameters();
            if (firstParams.LongLength != parameters.LongLength)
                return false;
            for (long i = 0; i < firstParams.LongLength; i++)
                if (firstParams[i].ParameterType != parameters[i])
                    return false;
            return true;
        }

        /// <summary>
        /// Determines whether formal parameters of both methods are equal by type.
        /// </summary>
        /// <param name="method">The first method to compare.</param>
        /// <param name="other">The second method to compare.</param>
        /// <returns><see langword="true"/>, if both methods have the same number of formal parameters and parameters are equal by type; otherwise, <see langword="false"/>.</returns>
		public static bool SignatureEquals(this MethodBase method, MethodBase other)
            => method.SignatureEquals(other.GetParameterTypes());

        /// <summary>
        /// Determines whether formal parameters of both methods are equal by type
        /// and return types are also equal.
        /// </summary>
        /// <param name="method">The first method to compare.</param>
        /// <param name="other">The second method to compare.</param>
        /// <returns><see langword="true"/>, if both methods have the same number of formal parameters, parameters are equal by type and return types are equal; otherwise, <see langword="false"/>.</returns>
		public static bool SignatureEquals(this MethodInfo method, MethodInfo other)
            => SignatureEquals((MethodBase)method, other) && method.ReturnType == other.ReturnType;
    }
}