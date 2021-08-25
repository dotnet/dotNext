using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Threading;

    [Obsolete]
    [ExcludeFromCodeCoverage]
    internal sealed class TestDiscoveryService : HashSet<Uri>, IMemberDiscoveryService, IDisposable
    {
        private readonly AsyncAutoResetEvent trigger = new(false);

        private sealed class Watcher : Disposable
        {
            private readonly WeakReference<TestDiscoveryService> service;
            private readonly CancellationTokenSource cancellation;

            internal Watcher(TestDiscoveryService service)
            {
                this.service = new WeakReference<TestDiscoveryService>(service);
                cancellation = new CancellationTokenSource();
            }

            internal async void Start(Func<IReadOnlyCollection<Uri>, CancellationToken, Task> callback)
            {
                while (this.service.TryGetTarget(out var service) && !cancellation.IsCancellationRequested)
                {
                    await service.trigger.WaitAsync(cancellation.Token);
                    await callback(service, cancellation.Token);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (!cancellation.IsCancellationRequested)
                        cancellation.Cancel();
                    cancellation.Dispose();
                }

                base.Dispose(disposing);
            }
        }

        public ValueTask<IReadOnlyCollection<Uri>> DiscoverAsync(CancellationToken token)
            => new(this);

        internal void FinishEditing() => trigger.Set();

        public ValueTask<IDisposable> WatchAsync(Func<IReadOnlyCollection<Uri>, CancellationToken, Task> callback, CancellationToken token)
        {
            var watcher = new Watcher(this);
            watcher.Start(callback);
            return new ValueTask<IDisposable>(watcher);
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