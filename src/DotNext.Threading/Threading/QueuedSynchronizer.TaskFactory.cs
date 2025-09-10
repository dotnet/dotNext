namespace DotNext.Threading;

using Patterns;

partial class QueuedSynchronizer
{
    private interface IValueTaskFactory : ISupplier<TimeSpan, CancellationToken, ValueTask>,
        ISupplier<TimeSpan, CancellationToken, ValueTask<bool>>;
    
    private sealed class CanceledTaskFactory : IValueTaskFactory, ISingleton<CanceledTaskFactory>
    {
        public static CanceledTaskFactory Instance { get; } = new();
        
        private CanceledTaskFactory()
        {
        }

        ValueTask ISupplier<TimeSpan, CancellationToken, ValueTask>.Invoke(TimeSpan timeout, CancellationToken token)
            => ValueTask.FromCanceled(token);

        ValueTask<bool> ISupplier<TimeSpan, CancellationToken, ValueTask<bool>>.Invoke(TimeSpan timeout, CancellationToken token)
            => ValueTask.FromCanceled<bool>(token);
    }

    private sealed class QueuedSynchronizerDisposedException(string objectName) : ObjectDisposedException(objectName), IValueTaskFactory
    {
        ValueTask ISupplier<TimeSpan, CancellationToken, ValueTask>.Invoke(TimeSpan timeout, CancellationToken token)
            => ValueTask.FromException(this);

        ValueTask<bool> ISupplier<TimeSpan, CancellationToken, ValueTask<bool>>.Invoke(TimeSpan timeout, CancellationToken token)
            => ValueTask.FromException<bool>(this);
    }
    
    private sealed class TimedOutTaskFactory : IValueTaskFactory, ISingleton<TimedOutTaskFactory>
    {
        public static TimedOutTaskFactory Instance { get; } = new();
        
        private TimedOutTaskFactory()
        {
        }

        ValueTask ISupplier<TimeSpan, CancellationToken, ValueTask>.Invoke(TimeSpan timeout, CancellationToken token)
            => ValueTask.FromException(new TimeoutException());

        ValueTask<bool> ISupplier<TimeSpan, CancellationToken, ValueTask<bool>>.Invoke(TimeSpan timeout, CancellationToken token)
            => ValueTask.FromResult(false);
    }
    
    private sealed class InvalidTimeoutTaskFactory : IValueTaskFactory, ISingleton<InvalidTimeoutTaskFactory>
    {
        public static InvalidTimeoutTaskFactory Instance { get; } = new();

        private InvalidTimeoutTaskFactory()
        {
        }
        
        ValueTask ISupplier<TimeSpan, CancellationToken, ValueTask>.Invoke(TimeSpan timeout, CancellationToken token)
            => ValueTask.FromException(new ArgumentOutOfRangeException(nameof(timeout)));

        ValueTask<bool> ISupplier<TimeSpan, CancellationToken, ValueTask<bool>>.Invoke(TimeSpan timeout, CancellationToken token)
            => ValueTask.FromException<bool>(new ArgumentOutOfRangeException(nameof(timeout)));
    }

    private sealed class CompletedTaskFactory : IValueTaskFactory, ISingleton<CompletedTaskFactory>
    {
        public static CompletedTaskFactory Instance { get; } = new();

        ValueTask ISupplier<TimeSpan, CancellationToken, ValueTask>.Invoke(TimeSpan timeout, CancellationToken token)
            => ValueTask.CompletedTask;

        ValueTask<bool> ISupplier<TimeSpan, CancellationToken, ValueTask<bool>>.Invoke(TimeSpan timeout, CancellationToken token)
            => ValueTask.FromResult(true);
    }
}