using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Replication;

using Threading;

internal static class CommitEvent
{
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    internal static async ValueTask WaitForCommitAsync<T>(this AsyncManualResetEvent commitEvent, Func<T, long, bool> commitChecker, T arg, long index, CancellationToken token)
        where T : class
    {
        if (index < 0L)
            throw new ArgumentOutOfRangeException(nameof(index));

        while (!commitChecker(arg, index))
            await commitEvent.WaitAsync(commitChecker, arg, index, token).ConfigureAwait(false);
    }
}