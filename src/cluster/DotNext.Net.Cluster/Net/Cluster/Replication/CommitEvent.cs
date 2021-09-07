namespace DotNext.Net.Cluster.Replication
{
    using Threading;

    internal static class CommitEvent
    {
        internal static async ValueTask<bool> WaitForCommitAsync<T>(this AsyncManualResetEvent commitEvent, Func<T, long, bool> commitChecker, T arg, long index, TimeSpan timeout, CancellationToken token)
            where T : class
        {
            if (index < 0L)
                throw new ArgumentOutOfRangeException(nameof(index));

            for (var timeoutMeasurement = new Timeout(timeout); !commitChecker(arg, index); await commitEvent.WaitAsync(commitChecker, arg, index, timeout, token).ConfigureAwait(false))
            {
                if (!timeoutMeasurement.RemainingTime.TryGetValue(out timeout))
                    return false;
            }

            return true;
        }
    }
}