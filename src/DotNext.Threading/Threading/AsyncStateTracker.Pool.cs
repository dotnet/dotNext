namespace DotNext.Threading;

using Tasks.Pooling;

partial class AsyncStateTracker
{
    private ValueTaskPool<bool> pool;

    private void ReturnToPool(WaitNode node)
    {
        lock (syncRoot)
        {
            if (!completed)
            {
                pool.Return(node);
            }
        }
    }
}