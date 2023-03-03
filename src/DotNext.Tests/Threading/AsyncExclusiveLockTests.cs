using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncExclusiveLockTests : Test
    {
        [Fact]
        public static async Task TrivialLock()
        {
            using var @lock = new AsyncExclusiveLock(3);
            True(await @lock.TryAcquireAsync(TimeSpan.FromMilliseconds(10)));
            False(await @lock.TryAcquireAsync(TimeSpan.FromMilliseconds(100)));
            await ThrowsAsync<TimeoutException>(@lock.AcquireAsync(TimeSpan.FromMilliseconds(100)).AsTask);
            @lock.Release();
            True(await @lock.TryAcquireAsync(TimeSpan.FromMilliseconds(100)));
        }

        [Fact]
        public static async Task ConcurrentLock()
        {
            var are = new TaskCompletionSource();
            using var @lock = new AsyncExclusiveLock();
            await @lock.AcquireAsync(TimeSpan.Zero);
            var task = Task.Run(async () =>
            {
                False(await @lock.TryAcquireAsync(TimeSpan.FromMilliseconds(10)));
                True(ThreadPool.QueueUserWorkItem(static ev => ev.SetResult(), are, false));
                await @lock.AcquireAsync(DefaultTimeout);
                @lock.Release();
                return true;
            });

            await are.Task.WaitAsync(DefaultTimeout);
            @lock.Release();
            True(await task);
        }

        [Fact]
        public static void FailFastLock()
        {
            using var @lock = new AsyncExclusiveLock();
            True(@lock.TryAcquire());
            True(@lock.IsLockHeld);
            False(@lock.TryAcquire());
            @lock.Release();
        }

        [Fact]
        public static async Task CancelSuspendedCallers()
        {
            using var @lock = new AsyncExclusiveLock();
            True(@lock.TryAcquire());
            var waitNode = @lock.AcquireAsync();
            False(waitNode.IsCompleted);
            Throws<ArgumentOutOfRangeException>(() => @lock.CancelSuspendedCallers(new CancellationToken(false)));
            @lock.CancelSuspendedCallers(new CancellationToken(true));
            await ThrowsAsync<OperationCanceledException>(waitNode.AsTask);
        }

        [Fact]
        public static void CallDisposeTwice()
        {
            var @lock = new AsyncExclusiveLock();
            @lock.Dispose();
            True(@lock.DisposeAsync().IsCompletedSuccessfully);
        }

        [Fact]
        public static void DisposeAsyncCompletedSynchronously()
        {
            using var @lock = new AsyncExclusiveLock();
            True(@lock.DisposeAsync().IsCompletedSuccessfully);
        }

        [Fact]
        public static async Task GracefulShutdown()
        {
            using var @lock = new AsyncExclusiveLock();
            True(@lock.TryAcquire());
            var task = @lock.DisposeAsync();
            False(task.IsCompleted);
            @lock.Release();
            await task;
            Throws<ObjectDisposedException>(() => @lock.TryAcquire());
        }

        [Fact]
        public static async Task GracefulShutdown2()
        {
            using var @lock = new AsyncExclusiveLock();
            True(@lock.TryAcquire());
            var task = @lock.DisposeAsync();
            False(task.IsCompleted);
            await ThrowsAsync<ObjectDisposedException>(@lock.AcquireAsync(CancellationToken.None).AsTask);
        }

        [Fact]
        public static async Task DisposedState()
        {
            var l = new AsyncExclusiveLock();
            l.Dispose();
            var result = l.TryAcquireAsync(System.Threading.Timeout.InfiniteTimeSpan);
            await ThrowsAsync<ObjectDisposedException>(result.AsTask);
        }

        [Fact]
        public static async Task CaptureCallerInfo()
        {
            using var l = new AsyncExclusiveLock();
            Empty(l.GetSuspendedCallers());

            l.TrackSuspendedCallers();
            await l.AcquireAsync();
            Empty(l.GetSuspendedCallers());

            using var activity = new Activity("MyOperation").Start();
            var suspendedTask = l.AcquireAsync();
            False(suspendedTask.IsCompleted);
            NotEmpty(l.GetSuspendedCallers());
            Equal("MyOperation", l.GetSuspendedCallers()[0] is Activity a ? a.OperationName : string.Empty);

            l.Release();
            await suspendedTask;
        }

        [Fact]
        public static async Task LockStealing()
        {
            const string reason = "Hello, world!";
            using var l = new AsyncExclusiveLock();
            True(await l.TryAcquireAsync(DefaultTimeout));

            var task1 = l.TryAcquireAsync(DefaultTimeout).AsTask();
            var task2 = l.AcquireAsync().AsTask();
            var task3 = l.TryStealAsync(reason, DefaultTimeout).AsTask();

            Same(reason, (await ThrowsAsync<PendingTaskInterruptedException>(Func.Constant(task1))).Reason);
            Same(reason, (await ThrowsAsync<PendingTaskInterruptedException>(Func.Constant(task2))).Reason);

            l.Release();
            True(await task3);
        }

        [Fact]
        public static async Task LockStealing2()
        {
            const string reason = "Hello, world!";
            using var l = new AsyncExclusiveLock();
            True(await l.TryAcquireAsync(DefaultTimeout));

            var task1 = l.TryAcquireAsync(DefaultTimeout).AsTask();
            var task2 = l.AcquireAsync().AsTask();
            var task3 = l.StealAsync(reason, DefaultTimeout).AsTask();

            Same(reason, (await ThrowsAsync<PendingTaskInterruptedException>(Func.Constant(task1))).Reason);
            Same(reason, (await ThrowsAsync<PendingTaskInterruptedException>(Func.Constant(task2))).Reason);

            l.Release();
            await task3;
        }
    }
}