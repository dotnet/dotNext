using System;
using System.Reflection;

namespace MissingPieces.Reflection
{
	/// <summary>
	/// Various extension methods for method reflection.
	/// </summary>
	public static class Methods
	{
		public static Type[] GetParameterTypes(this MethodBase method)
            => method?.GetParameters().Map(p => p.ParameterType);

		public static bool SignatureEquals(this MethodBase method, MethodBase other)
		{
			var firstParams = method.GetParameters();
			var secondParams = method.GetParameters();
			if(firstParams.LongLength != secondParams.LongLength)
				return false;
			for(long i = 0; i < firstParams.LongLength; i++)
				if(firstParams[i].ParameterType != secondParams[i].ParameterType)
					return false;
			return true;
		}

		public static bool SignatureEquals(this MethodInfo method, MethodInfo other)
			=> SignatureEquals(method.Upcast<MethodBase, MethodInfo>(), other) && method.ReturnType == other.ReturnType;
	}
}