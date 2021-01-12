namespace DotNext
{
    internal static class ArrayExtensions
    {
        internal static long Take<T>(this T[] array, out T first, out T second, int startIndex = 0)
        {
            if (array.ElementAt(startIndex, out first))
                startIndex += 1;
            else
            {
                second = default;
                return 0;
            }
            return array.ElementAt(startIndex, out second) ? 2L : 1L;
        }

        internal static long Take<T>(this T[] array, out T first, out T second, out T third, int startIndex = 0)
        {
            if (array.ElementAt(startIndex, out first))
                startIndex += 1;
            else
            {
                second = third = default;
                return 0L;
            }

            if (array.ElementAt(startIndex, out second))
                startIndex += 1;
            else
            {
                third = default;
                return 1L;
            }
            return array.ElementAt(startIndex, out third) ? 3L : 2L;
        }
    }
}
