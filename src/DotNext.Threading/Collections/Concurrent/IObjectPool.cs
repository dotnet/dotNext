namespace DotNext.Collections.Concurrent;

internal interface IObjectPool<T>
    where T : class
{
    T? TryRent();

    void Return(T item);
}