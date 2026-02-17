namespace DotNext.Collections.Concurrent;

internal interface IObjectPool<T>
    where T : class
{
    T? TryGet();

    void Return(T item);
}