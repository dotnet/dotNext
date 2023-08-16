namespace DotNext.Collections.Specialized;

internal static class ConcurrentTypeMapExtensions
{
    internal static T RemoveOrCreate<T>(this ConcurrentTypeMap map)
        where T : class, new()
    {
        if (!map.Remove(out T? result))
            result = new();

        return result;
    }
}