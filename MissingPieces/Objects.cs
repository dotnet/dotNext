namespace MissingPieces
{
	public static class Objects
	{
		public static bool OneOf<T>(this T value, params T[] values)
			where T: class
		{
			foreach (var v in values)
				if (Equals(value, v))
					return true;
			return false;
		}
	}
}
