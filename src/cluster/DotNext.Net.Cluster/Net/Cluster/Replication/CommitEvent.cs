using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    using IO.Log;
    using Threading;

    internal static class CommitEvent
    {
        internal static async Task WaitForCommitAsync(IAuditTrail log, IAsyncEvent commitEvent, long index, TimeSpan timeout, CancellationToken token)
        {
            for (var timeoutMeasurement = new Timeout(timeout); log.GetLastIndex(true) < index; await commitEvent.WaitAsync(timeout, token).ConfigureAwait(false))
                timeoutMeasurement.ThrowIfExpired(out timeout);
        }
    }
}