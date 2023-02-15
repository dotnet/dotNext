using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Replication;

using Threading;

internal sealed class CommitEvent : AsyncManualResetEvent
{
    internal CommitEvent()
        : base(initialState: false)
    {
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    internal async ValueTask WaitForCommitAsync<T>(Func<T, long, bool> commitChecker, T arg, long index, CancellationToken token)
        where T : class
    {
        if (index < 0L)
            throw new ArgumentOutOfRangeException(nameof(index));

        while (!commitChecker(arg, index))
            await WaitAsync(commitChecker, arg, index, token).ConfigureAwait(false);
    }
}