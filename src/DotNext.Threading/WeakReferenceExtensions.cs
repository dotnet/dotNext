namespace DotNext;

internal static class WeakReferenceExtensions
{
    internal static void Consume<T, TConsumer>(this WeakReference<TConsumer?> weakRef, T obj)
        where TConsumer : class, IConsumer<T>
    {
        if (weakRef.TryGetTarget(out var consumer))
            consumer.Invoke(obj);
    }
}