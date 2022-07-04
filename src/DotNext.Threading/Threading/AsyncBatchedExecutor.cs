namespace DotNext.Threading;

/// <summary>
/// Asynchronous batched executor
/// </summary>
public static class AsyncBatchedExecutor
{
    /// <summary>
    /// Invoke asynchronous batched execution
    /// </summary>
    /// <param name="collection">Collection of elements.</param>
    /// <param name="batchCount">Batch size.</param>
    /// <param name="action">Async delegate wich will be invoked for each element in collection.</param>
    /// <typeparam name="T">Type of elements.</typeparam>
    /// <returns>Task.</returns>
    public static async Task ExecuteBatched<T>(this IEnumerable<T> collection, int batchCount, Func<T, Task> action)
    {
        var executingTasks = new Task[batchCount];

        using var enumerator = collection.GetEnumerator();

        var id = 0;
        for (; id < batchCount && enumerator.MoveNext(); id++)
            executingTasks[id] = action(enumerator.Current);

        if (id < batchCount)
        {
            await Task.WhenAll(executingTasks[..id]).ConfigureAwait(false);
            return;
        }

        while (enumerator.MoveNext())
        {
            await Task.WhenAll(executingTasks).ConfigureAwait(false);

            for (var i = 0; i < executingTasks.Length; i++)
            {
                if (executingTasks[i].IsCompleted is false)
                    continue;

                executingTasks[i] = action(enumerator.Current);

                if (i == executingTasks.Length - 1 || enumerator.MoveNext() is false)
                    break;
            }
        }

        await Task.WhenAll(executingTasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Invoke asynchronous batched execution
    /// </summary>
    /// <param name="collection">Collection of elements.</param>
    /// <param name="batchCount">Batch size.</param>
    /// <param name="func">Async delegate wich will be invoked for each element in collection.</param>
    /// <typeparam name="TIn">Type of input elements in colection.</typeparam>
    /// <typeparam name="TOut">Type of returned elements.</typeparam>
    /// <returns>Enumerable of elements as a result of func calls.</returns>
    public static async IAsyncEnumerable<TOut> ExecuteBatchedWithResult<TIn, TOut>(this IEnumerable<TIn> collection, int batchCount, Func<TIn, Task<TOut>> func)
    {
        var executingTasks = new Task<TOut>[batchCount];

        using var enumerator = collection.GetEnumerator();

        var id = 0;
        for (; id < batchCount && enumerator.MoveNext(); id++)
            executingTasks[id] = func(enumerator.Current);

        if (id < batchCount)
        {
            var tasksNeeded = executingTasks[..id];
            await Task.WhenAll(tasksNeeded).ConfigureAwait(false);
            foreach (var task in tasksNeeded)
                yield return task.Result;

            yield break;
        }

        while (enumerator.MoveNext())
        {
            await Task.WhenAll(executingTasks).ConfigureAwait(false);

            for (var i = 0; i < executingTasks.Length; i++)
            {
                if (executingTasks[i].IsCompleted is false)
                    continue;

                yield return executingTasks[i].Result;

                executingTasks[i] = func(enumerator.Current);

                if (i == executingTasks.Length - 1 || enumerator.MoveNext() is false)
                    break;
            }
        }

        await Task.WhenAll(executingTasks).ConfigureAwait(false);
        foreach (var task in executingTasks)
            yield return task.Result;
    }
}