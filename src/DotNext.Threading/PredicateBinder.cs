namespace DotNext;

internal static class PredicateBinder
{
    internal static bool Check<T>(this Predicate<T> predicate, object state)
        => state is T obj && predicate(obj);
}