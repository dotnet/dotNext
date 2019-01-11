using System;
using System.Reflection;

namespace DotNext.Reflection
{
	/// <summary>
	/// Various extension methods for method reflection.
	/// </summary>
	public static class Methods
	{
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
			=> SignatureEquals(method.Upcast<MethodBase, MethodInfo>(), other) && method.ReturnType == other.ReturnType;
	}
}