using System.Runtime.CompilerServices;

namespace MissingPieces
{
	public static class ValueTypes
	{
		public static bool BiwiseEquals<T>(in T first, in T second)
			where T: struct
		{
			return false;
		}
	}
}
