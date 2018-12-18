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

		public static bool SignatureEquals(this MethodInfo method, MethodInfo other)
		{
			var firstParams = method.GetParameters();
			var secondParams = method.GetParameters();
			if(firstParams.LongLength != secondParams.LongLength)
				return false;
			for(long i = 0; i < firstParams.LongLength; i++)
				if(firstParams[i].ParameterType != secondParams[i].ParameterType)
					return false;
			return method.ReturnType == other.ReturnType;
		}
	}
}