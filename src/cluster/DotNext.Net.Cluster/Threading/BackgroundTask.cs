using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    internal readonly struct BackgroundTask : IDisposable
    {
        private readonly CancellationTokenSource cts;
        private readonly Task backgroundTask;

        internal BackgroundTask(Func<CancellationToken, Task> task)
        {
            cts = new CancellationTokenSource();
            backgroundTask = task(cts.Token);
        }

        internal CancellationTokenSource CreateLinkedTokenSource(CancellationToken token)
            => CancellationTokenSource.CreateLinkedTokenSource(cts.Token, token);

        internal Task Stop(CancellationToken token)
        {
            if (cts is null || backgroundTask is null)
                return Task.CompletedTask;
            cts.Cancel(false);
            return Task.WhenAny(backgroundTask, Task.Delay(System.Threading.Timeout.Infinite, token));
        }

        internal Task Stop()
        {
            if (cts is null || backgroundTask is null)
                return Task.CompletedTask;
            cts.Cancel(false);
            return backgroundTask;
        }

        public void Dispose()
        {
            Disposable.Dispose(backgroundTask, cts);
        }
    }
}
