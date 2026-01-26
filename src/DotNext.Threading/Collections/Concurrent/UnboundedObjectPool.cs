using System.Collections.Concurrent;

namespace DotNext.Collections.Concurrent;

internal sealed class UnboundedObjectPool<T> : ConcurrentQueue<T>, IObjectPool<T>
    where T : class
{
    private T? fastItem;

    public T? TryGet()
    {
        if (fastItem is not { } result
            || Interlocked.CompareExchange(ref fastItem, null, result) != result)
        {
            TryDequeue(out result);
        }

        return result;
    }

    public void Return(T item)
    {
        if (fastItem is not null || Interlocked.CompareExchange(ref fastItem, item, null) is not null)
        {
            Enqueue(item);
        }
    }
}