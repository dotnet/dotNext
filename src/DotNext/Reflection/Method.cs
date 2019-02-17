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
            => method?.GetParameters().Convert(p => p.ParameterType);

		public static bool SignatureEquals(this MethodBase method, Type[] parameters)
		{
			var firstParams = method.GetParameters();
			if(firstParams.LongLength != parameters.LongLength)
				return false;
			for(long i = 0; i < firstParams.LongLength; i++)
				if(firstParams[i].ParameterType != parameters[i])
					return false;
			return true;
		}

		public static bool SignatureEquals(this MethodBase method, MethodBase other)
			=> method.SignatureEquals(other.GetParameterTypes());

		public static bool SignatureEquals(this MethodInfo method, MethodInfo other)
			=> SignatureEquals((MethodBase)method, other) && method.ReturnType == other.ReturnType;
	}
}