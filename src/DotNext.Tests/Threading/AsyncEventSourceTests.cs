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
            source.Fire();
            var task = listener.WaitAsync();
            True(task.IsCompleted);
            True(task.IsCompletedSuccessfully);
            task = listener.WaitAsync();
            False(task.IsCompleted);
            False(task.IsCompletedSuccessfully);
        }

        [Fact]
        public static async Task FireAndWaitAsync()
        {
            var source = new AsyncEventSource();
            await using var listener = new AsyncEventListener(source);
            await source.FireAsync();
            var task = listener.WaitAsync();
            True(task.IsCompleted);
            True(task.IsCompletedSuccessfully);
            task = listener.WaitAsync();
            False(task.IsCompleted);
            False(task.IsCompletedSuccessfully);
        }

        [Fact]
        public static void Cancellation()
        {
            var token = new CancellationTokenSource();
            var source = new AsyncEventSource();
            using var listener = new AsyncEventListener(source, token.Token);
            source.Fire();
            var task = listener.WaitAsync();
            True(task.IsCompleted);
            True(task.IsCompletedSuccessfully);
            token.Cancel();
            Throws<OperationCanceledException>(() => listener.WaitAsync());
        }

        [Fact]
        public static async Task AsyncSignal()
        {
            static void Fire(AsyncEventSource source)
                => source.Fire();

            var source = new AsyncEventSource();
            var listener = new AsyncEventListener(source);
            var task = listener.WaitAsync();
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
                    await listener.WaitAsync();
                return result;
            }

            var source = new AsyncEventSource();
            var counter1 = ExecuteCounter(source);
            var counter2 = ExecuteCounter(source);
            int value;
            for(value = 0; !(counter1.IsCompleted && counter2.IsCompleted); value++, await Task.Delay(100))
                source.Fire();
            Equal(3, await counter1);
            Equal(3, await counter2);
            True(value >= 3);
        }
    }
}