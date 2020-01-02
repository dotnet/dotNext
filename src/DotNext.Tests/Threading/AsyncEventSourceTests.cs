using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    public sealed class AsyncEventSourceTests : Assert
    {
        [Fact]
        public static void FireAndWait()
        {
            var source = new AsyncEventSource();
            using var listener = new AsyncEventListener(source);
            source.Resume();
            var task = listener.SuspendAsync();
            True(task.IsCompleted);
            True(task.IsCompletedSuccessfully);
            task = listener.SuspendAsync();
            False(task.IsCompleted);
            False(task.IsCompletedSuccessfully);
        }

        [Fact]
        public static async Task FireAndWaitAsync()
        {
            var source = new AsyncEventSource();
            await using var listener = new AsyncEventListener(source);
            await source.ResumeAsync();
            var task = listener.SuspendAsync();
            True(task.IsCompleted);
            True(task.IsCompletedSuccessfully);
            task = listener.SuspendAsync();
            False(task.IsCompleted);
            False(task.IsCompletedSuccessfully);
        }

        [Fact]
        public static void Cancellation()
        {
            var token = new CancellationTokenSource();
            var source = new AsyncEventSource();
            using var listener = new AsyncEventListener(source, token.Token);
            source.Resume();
            var task = listener.SuspendAsync();
            True(task.IsCompleted);
            True(task.IsCompletedSuccessfully);
            token.Cancel();
            Throws<OperationCanceledException>(() => listener.SuspendAsync());
        }

        [Fact]
        public static async Task AsyncSignal()
        {
            static void Fire(AsyncEventSource source)
                => source.Resume();

            var source = new AsyncEventSource();
            var listener = new AsyncEventListener(source);
            var task = listener.SuspendAsync();
            False(task.IsCompleted);
            ThreadPool.QueueUserWorkItem(Fire, source, false);
            await task;
            True(task.IsCompleted);
        }

        [Fact]
        public static async Task Counter()
        {
            static async Task<int> ExecuteCounter(AsyncEventSource source)
            {
                var result = 0;
                using var listener = new AsyncEventListener(source);
                for(; result < 3; result++)
                    await listener.SuspendAsync();
                return result;
            }

            var source = new AsyncEventSource();
            var counter1 = ExecuteCounter(source);
            var counter2 = ExecuteCounter(source);
            int value;
            for(value = 0; !(counter1.IsCompleted && counter2.IsCompleted); value++, await Task.Delay(100))
                source.Resume();
            Equal(3, await counter1);
            Equal(3, await counter2);
            True(value >= 3);
        }
    }
}