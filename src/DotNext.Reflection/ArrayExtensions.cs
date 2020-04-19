using System.Diagnostics.CodeAnalysis;

namespace DotNext
{
    internal static class ArrayExtensions
    {
        internal static bool Take<T>(this T[] array, [NotNullWhen(true)] out T first, [NotNullWhen(true)] out T second, int startIndex = 0)
            where T : notnull
        {
            if (startIndex + 1 < array.LongLength)
            {
                first = array[startIndex++];
                second = array[startIndex];
                return true;
            }
            else
            {
                first = second = default!;
                return false;
            }
        }

        internal static bool Take<T>(this T[] array, [NotNullWhen(true)] out T first, [NotNullWhen(true)] out T second, [NotNullWhen(true)]out T third, int startIndex = 0)
            where T : notnull
        {
            if (startIndex + 2 < array.LongLength)
            {
                first = array[startIndex++];
                second = array[startIndex++];
                third = array[startIndex];
                return true;
            }
            else
            {
                first = second = third = default!;
                return false;
            }
        }
    }
}
