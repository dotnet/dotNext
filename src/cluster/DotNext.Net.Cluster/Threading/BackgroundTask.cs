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

        internal Task Stop(CancellationToken token)
        {
            if (cts is null || backgroundTask is null)
                return Task.CompletedTask;
            cts.Cancel(false);
            return Task.WhenAny(backgroundTask, Task.Delay(System.Threading.Timeout.Infinite, token));
        }

        public void Dispose()
        {
            Disposable.Dispose(backgroundTask, cts);
        }
    }
}
