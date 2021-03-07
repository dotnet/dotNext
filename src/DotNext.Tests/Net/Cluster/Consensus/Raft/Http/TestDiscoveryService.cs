using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Threading;

    internal sealed class TestDiscoveryService : HashSet<Uri>, IMemberDiscoveryService, IDisposable
    {
        private readonly AsyncAutoResetEvent trigger = new AsyncAutoResetEvent(false);

        public ValueTask<IReadOnlyCollection<Uri>> DiscoverAsync(CancellationToken token)
            => new ValueTask<IReadOnlyCollection<Uri>>(this);

        internal void FinishEditing() => trigger.Set();

        public async Task WatchAsync(Func<IReadOnlyCollection<Uri>, CancellationToken, Task> callback, CancellationToken token)
        {
            for (var canceled = false; ; )
            {
                try
                {
                    await trigger.WaitAsync(token);
                }
                catch (OperationCanceledException)
                {
                    // do not transfer control to the callback in this case
                    canceled = true;
                }

                if (canceled)
                    break;
                else
                    await callback(this, token);
            }
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                trigger.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~TestDiscoveryService() => Dispose(false);
    }
}